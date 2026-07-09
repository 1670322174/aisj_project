// 作用：实现 AI 生成结果保存逻辑。
// 负责从 Provider 下载输出文件、上传到 COS、写入 aigenerationjobimages；不负责提交工作流。

using System.Text.Json;
using InteriorDesignWeb.Data;
using InteriorDesignWeb.Models.DTOs.AI;
using InteriorDesignWeb.Models.Entities;
using InteriorDesignWeb.Providers.AI;

namespace InteriorDesignWeb.Services.AI;

public class AIResultService : IAIResultService
{
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
        var resultItems = new List<AIResultImageDto>();

        foreach (var output in outputs)
        {
            await using var outputStream = await _provider.DownloadOutputAsync(output, cancellationToken);
            var cosResult = await _cosService.UploadAIImageAsync(
                outputStream,
                output.FileName,
                workflow.OutputType == "video" ? "AI-Generated-Video" : "AI-Generated");

            string? thumbnailPath = null;

            if (IsImageOutput(output))
            {
                try
                {
                    outputStream.Position = 0;
                    thumbnailPath = await _cosService.GenerateAndUploadThumbnailAsync(outputStream, cosResult.Path);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "AI 结果缩略图生成失败，不影响主结果保存。JobId={JobId}, File={File}",
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
                SourceType = workflow.OutputType,
                MetadataJson = JsonSerializer.Serialize(new
                {
                    provider = workflow.ProviderType,
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
                IsAddedToProject = false
            };

            await _context.aigenerationjobimages.AddAsync(entity, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            resultItems.Add(new AIResultImageDto
            {
                AiImageID = entity.AiImageID,
                ImageUrl = entity.ImageUrl ?? string.Empty,
                CosPath = entity.CosPath ?? string.Empty,
                ThumbnailPath = entity.ThumbnailPath,
                MediaType = output.MediaType
            });
        }

        return resultItems;
    }

    private static bool IsImageOutput(AIProviderOutput output)
    {
        var extension = Path.GetExtension(output.FileName).ToLowerInvariant();
        return output.MediaType.Equals("image", StringComparison.OrdinalIgnoreCase)
            || extension is ".png" or ".jpg" or ".jpeg" or ".webp";
    }
}
