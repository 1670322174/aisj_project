// 作用：保存 ComfyUI Server 生成结果。
// 负责从 ComfyUI Server 下载图片或视频、上传到 COS、写入 aigenerationjobimages，并避免同一任务重复入库。

using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using InteriorDesignWeb.Data;
using InteriorDesignWeb.Models.DTOs.AI;
using InteriorDesignWeb.Models.Entities;
using InteriorDesignWeb.Providers.AI;
using Microsoft.EntityFrameworkCore;

namespace InteriorDesignWeb.Services.AI;

public sealed class AIResultService : IAIResultService
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> JobSaveLocks =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly IAIProvider _provider;
    private readonly CosService _cosService;
    private readonly DesignHubContext _context;
    private readonly ILogger<AIResultService> _logger;

    public AIResultService(
        IAIProvider provider,
        CosService cosService,
        DesignHubContext context,
        ILogger<AIResultService> logger)
    {
        _provider = provider;
        _cosService = cosService;
        _context = context;
        _logger = logger;
    }

    public async Task<IReadOnlyList<AIResultImageDto>> SaveProviderOutputsAsync(
        string jobId,
        int? userId,
        WorkflowDefinition workflow,
        AIGenerationSubmitRequest request,
        IReadOnlyList<AIProviderOutput> outputs,
        CancellationToken cancellationToken = default)
    {
        var saveLock = JobSaveLocks.GetOrAdd(jobId, _ => new SemaphoreSlim(1, 1));
        await saveLock.WaitAsync(cancellationToken);

        try
        {
        var selectedOutputs = SelectWorkflowOutputs(workflow, outputs);
        var existing = await _context.aigenerationjobimages
            .Where(item => item.JobId == jobId)
            .OrderBy(item => item.CreatedAt)
            .ToListAsync(cancellationToken);

        // 后台轮询与手动查询可能同时触发同步；已有结果时直接返回，避免重复上传 COS。
        if (existing.Count > 0)
        {
            return existing.Select(ToResultDto).ToList();
        }

        var resultItems = new List<AIResultImageDto>();

        foreach (var output in selectedOutputs)
        {
            await using var outputStream = await _provider.DownloadOutputAsync(
                output,
                cancellationToken);

            var category = output.MediaType == "video"
                ? "AI-Generated-Video"
                : "AI-Generated";

            var cosResult = await _cosService.UploadAIImageAsync(
                outputStream,
                output.FileName,
                category);

            string? thumbnailPath = null;
            if (IsImageOutput(output))
            {
                try
                {
                    outputStream.Position = 0;
                    thumbnailPath = await _cosService.GenerateAndUploadThumbnailAsync(
                        outputStream,
                        cosResult.Path);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "AI结果缩略图生成失败，不影响主结果保存。JobId={JobId}, File={File}",
                        jobId,
                        output.FileName);
                }
            }

            var entity = new AiGenerationJobImage
            {
                JobId = jobId,
                UserID = userId,
                ImageUrl = cosResult.Url,
                CosPath = cosResult.Path,
                ThumbnailPath = thumbnailPath,
                WorkflowCode = workflow.WorkflowCode,
                ModelCode = request.ModelCode ?? workflow.DefaultModelCode,
                Prompt = request.Prompt,
                SourceType = output.MediaType,
                OutputKey = BuildOutputKey(output),
                MetadataJson = JsonSerializer.Serialize(new
                {
                    provider = _provider.ProviderType,
                    workflowCode = workflow.WorkflowCode,
                    output.NodeId,
                    output.FileName,
                    output.Subfolder,
                    output.Type,
                    output.MediaType,
                    request.ProjectId,
                    request.RoomId
                }),
                CreatedAt = DateTime.UtcNow,
                IsAddedToProject = false,
                RetentionStatus = AiGenerationJobImage.RetentionActive
            };

            await _context.aigenerationjobimages.AddAsync(entity, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            resultItems.Add(ToResultDto(entity));
        }

        return resultItems;
        }
        finally
        {
            saveLock.Release();
        }
    }

    private IReadOnlyList<AIProviderOutput> SelectWorkflowOutputs(
        WorkflowDefinition workflow,
        IReadOnlyList<AIProviderOutput> outputs)
    {
        var outputNodeIds = workflow.OutputNodeIds
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var selected = outputNodeIds.Count == 0
            ? outputs.ToList()
            : outputs.Where(output => outputNodeIds.Contains(output.NodeId)).ToList();

        if (selected.Count == 0 && outputs.Count > 0)
        {
            _logger.LogWarning(
                "工作流正式输出节点未返回文件，暂时使用 Provider 全部输出。Workflow={Workflow}, Nodes={Nodes}",
                workflow.WorkflowCode,
                string.Join(',', workflow.OutputNodeIds));
            selected = outputs.ToList();
        }

        return selected
            .GroupBy(BuildOutputKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    private static string BuildOutputKey(AIProviderOutput output)
    {
        var identity = string.Join('|',
            output.NodeId,
            output.FileName,
            output.Subfolder ?? string.Empty,
            string.IsNullOrWhiteSpace(output.Type) ? "output" : output.Type);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)))
            .ToLowerInvariant();
    }

    private static AIResultImageDto ToResultDto(AiGenerationJobImage entity)
    {
        return new AIResultImageDto
        {
            AiImageID = entity.AiImageID,
            ImageUrl = entity.ImageUrl ?? string.Empty,
            CosPath = entity.CosPath ?? string.Empty,
            ThumbnailPath = entity.ThumbnailPath,
            MediaType = entity.SourceType
        };
    }

    private static bool IsImageOutput(AIProviderOutput output)
    {
        var extension = Path.GetExtension(output.FileName).ToLowerInvariant();
        return output.MediaType.Equals("image", StringComparison.OrdinalIgnoreCase)
            || extension is ".png" or ".jpg" or ".jpeg" or ".webp";
    }
}
