// 作用：统一编排 7 个 AI 工作流的 ComfyUI Server 执行流程。
// 流程：校验工作流 -> 构建 API 工作流 -> 创建 AIJob -> 提交 ComfyUI Server -> 轮询状态 -> 下载结果 -> 上传 COS -> 写入结果表。

using System.Text.Json;
using System.Text.Json.Serialization;
using InteriorDesignWeb.Application.Common;
using InteriorDesignWeb.Config;
using InteriorDesignWeb.Data;
using InteriorDesignWeb.Models.DTOs.AI;
using InteriorDesignWeb.Models.Entities;
using InteriorDesignWeb.Providers.AI;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace InteriorDesignWeb.Services.AI;

public sealed class AIGenerationService : IAIGenerationService
{
    private readonly IWorkflowRegistry _workflowRegistry;
    private readonly IWorkflowBuilder _workflowBuilder;
    private readonly IAIProvider _provider;
    private readonly DesignHubContext _context;
    private readonly IAIResultService _resultService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ComfyUIServerOptions _serverOptions;
    private readonly ILogger<AIGenerationService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public AIGenerationService(
        IWorkflowRegistry workflowRegistry,
        IWorkflowBuilder workflowBuilder,
        IAIProvider provider,
        DesignHubContext context,
        IAIResultService resultService,
        IServiceScopeFactory scopeFactory,
        IOptions<ComfyUIServerOptions> serverOptions,
        ILogger<AIGenerationService> logger)
    {
        _workflowRegistry = workflowRegistry;
        _workflowBuilder = workflowBuilder;
        _provider = provider;
        _context = context;
        _resultService = resultService;
        _scopeFactory = scopeFactory;
        _serverOptions = serverOptions.Value;
        _logger = logger;
    }

    public IReadOnlyList<WorkflowOptionDto> GetWorkflowOptions()
    {
        return _workflowRegistry.GetAll()
            .Select(workflow => new WorkflowOptionDto
            {
                WorkflowCode = workflow.WorkflowCode,
                Name = workflow.Name,
                Description = workflow.Description,
                ProviderType = workflow.ProviderType,
                OutputType = workflow.OutputType,
                DefaultModelCode = workflow.DefaultModelCode,
                CostUnits = workflow.CostUnits,
                Enabled = workflow.Enabled,
                RequiredInputs = workflow.RequiredInputs,
                OptionalInputs = workflow.OptionalInputs
            })
            .ToList();
    }

    public async Task<ComfyUploadResponseDto> UploadInputImageAsync(
        IFormFile file,
        string fieldName,
        string? subfolder = null,
        bool overwrite = true,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fieldName))
        {
            throw AppException.Validation(
                "fieldName 不能为空，例如 sourceImage、referenceImage、firstFrame、lastFrame。");
        }

        var result = await _provider.UploadImageAsync(
            file,
            subfolder,
            overwrite,
            cancellationToken);

        return new ComfyUploadResponseDto
        {
            Name = result.Name,
            Subfolder = result.Subfolder,
            Type = result.Type,
            FieldName = fieldName
        };
    }

    public async Task<AIGenerationSubmitResponse> SubmitAsync(
        AIGenerationSubmitRequest request,
        int userId,
        CancellationToken cancellationToken = default)
    {
        var workflow = _workflowRegistry.GetRequired(request.WorkflowCode);
        request.ModelCode ??= workflow.DefaultModelCode;

        var supportsNegativePrompt = !workflow.OutputType.Equals(
                "video",
                StringComparison.OrdinalIgnoreCase)
            || workflow.InputMappings.Any(mapping => mapping.Field.Equals(
                "negativePrompt",
                StringComparison.OrdinalIgnoreCase));
        request.NegativePrompt = supportsNegativePrompt
            ? NegativePromptPolicy.Compose(request.NegativePrompt)
            : null;

        var workflowJson = _workflowBuilder.Build(workflow, request);
        var jobId = Guid.NewGuid().ToString("N");
        var inputJson = JsonSerializer.Serialize(request, JsonOptions);

        var job = new AiGenerationJob
        {
            JobId = jobId,
            PromptId = jobId,
            ProviderJobId = null,
            UserID = userId,
            WorkflowCode = workflow.WorkflowCode,
            ModelCode = request.ModelCode,
            ProviderType = _provider.ProviderType,
            Prompt = request.Prompt,
            NegativePrompt = request.NegativePrompt,
            ParametersJson = inputJson,
            InputJson = inputJson,
            Status = AIJobStatus.Created,
            Progress = "0",
            ProgressValue = 0,
            CostUnits = workflow.CostUnits,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _context.aigenerationjobs.AddAsync(job, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        try
        {
            job.Status = AIJobStatus.Queued;
            job.Progress = "1";
            job.ProgressValue = 1;
            job.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);

            var submitResult = await _provider.SubmitAsync(workflowJson, cancellationToken);

            job.PromptId = submitResult.ProviderJobId;
            job.ProviderJobId = submitResult.ProviderJobId;
            job.Status = AIJobStatus.Queued;
            job.Progress = "2";
            job.ProgressValue = 2;
            job.StartedAt = DateTime.UtcNow;
            job.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);

            // 请求返回后在独立作用域轮询 ComfyUI Server，避免继续占用前端请求线程。
            return new AIGenerationSubmitResponse
            {
                JobId = job.JobId,
                ProviderJobId = submitResult.ProviderJobId,
                PromptId = submitResult.ProviderJobId,
                WorkflowCode = workflow.WorkflowCode,
                ModelCode = job.ModelCode,
                OutputType = workflow.OutputType,
                Status = job.Status,
                ProgressValue = job.ProgressValue
            };
        }
        catch (Exception ex)
        {
            job.Status = AIJobStatus.Failed;
            job.ErrorCode = ErrorCodes.AiProviderError;
            job.ErrorMessage = ex.Message;
            job.CompletedAt = DateTime.UtcNow;
            job.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogError(
                ex,
                "AI任务提交到 ComfyUI Server 失败。JobId={JobId}, WorkflowCode={WorkflowCode}",
                job.JobId,
                workflow.WorkflowCode);

            throw;
        }
    }

    public async Task RefreshAsync(
        string jobId,
        int userId,
        CancellationToken cancellationToken = default)
    {
        var job = await _context.aigenerationjobs.FirstOrDefaultAsync(
            item => item.JobId == jobId && item.UserID == userId,
            cancellationToken);

        if (job == null)
        {
            throw new AppException(
                ErrorCodes.AiJobNotFound,
                "AI任务不存在或无权访问。",
                StatusCodes.Status404NotFound);
        }

        if (IsTerminal(job.Status) || string.IsNullOrWhiteSpace(job.ProviderJobId))
        {
            return;
        }

        var history = await _provider.GetHistoryAsync(
            job.ProviderJobId,
            cancellationToken);

        job.OutputJson = history.RawJson;
        job.UpdatedAt = DateTime.UtcNow;

        if (history.IsFailed)
        {
            job.Status = AIJobStatus.Failed;
            job.Progress = "100";
            job.ProgressValue = 100;
            job.ErrorCode = ErrorCodes.AiProviderError;
            job.ErrorMessage = history.ErrorMessage ?? "ComfyUI Server 任务执行失败。";
            job.CompletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
            return;
        }

        if (history.IsCancelled)
        {
            job.Status = AIJobStatus.Cancelled;
            job.Progress = "100";
            job.ProgressValue = 100;
            job.CompletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
            return;
        }

        if (!history.IsCompleted)
        {
            job.Status = history.Status == "in_progress"
                ? AIJobStatus.Running
                : AIJobStatus.Queued;
            job.ProgressValue = Math.Clamp(history.ProgressValue, 2, 95);
            job.Progress = job.ProgressValue.ToString();
            await _context.SaveChangesAsync(cancellationToken);
            return;
        }

        if (history.Outputs.Count == 0)
        {
            job.Status = AIJobStatus.Failed;
            job.Progress = "100";
            job.ProgressValue = 100;
            job.ErrorCode = ErrorCodes.AiProviderError;
            job.ErrorMessage = "ComfyUI Server 任务已完成，但没有返回可下载的输出文件。";
            job.CompletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
            return;
        }

        job.Status = AIJobStatus.Uploading;
        job.Progress = "96";
        job.ProgressValue = 96;
        await _context.SaveChangesAsync(cancellationToken);

        var workflow = _workflowRegistry.GetRequired(job.WorkflowCode);
        var request = DeserializeRequest(job.InputJson, job.ParametersJson);
        await _resultService.SaveProviderOutputsAsync(
            job.JobId,
            job.UserID,
            workflow,
            request,
            history.Outputs,
            cancellationToken);

        job.Status = AIJobStatus.Succeeded;
        job.Progress = "100";
        job.ProgressValue = 100;
        job.CompletedAt = DateTime.UtcNow;
        job.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> CancelAsync(
        string jobId,
        int userId,
        CancellationToken cancellationToken = default)
    {
        var job = await _context.aigenerationjobs.FirstOrDefaultAsync(
            item => item.JobId == jobId && item.UserID == userId,
            cancellationToken);

        if (job == null)
        {
            throw new AppException(
                ErrorCodes.AiJobNotFound,
                "AI任务不存在或无权访问。",
                StatusCodes.Status404NotFound);
        }

        if (IsTerminal(job.Status))
        {
            return job.Status == AIJobStatus.Cancelled;
        }

        if (string.IsNullOrWhiteSpace(job.ProviderJobId))
        {
            await MarkCancelledAsync(job, cancellationToken);
            return true;
        }

        var cancelled = await _provider.CancelAsync(job.ProviderJobId, cancellationToken);
        if (cancelled)
        {
            await MarkCancelledAsync(job, cancellationToken);
        }

        return cancelled;
    }

    private async Task MonitorAndSaveResultAsync(
        string jobId,
        string providerJobId,
        string workflowCode)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DesignHubContext>();
            var provider = scope.ServiceProvider.GetRequiredService<IAIProvider>();
            var resultService = scope.ServiceProvider.GetRequiredService<IAIResultService>();
            var workflowRegistry = scope.ServiceProvider.GetRequiredService<IWorkflowRegistry>();
            var workflow = workflowRegistry.GetRequired(workflowCode);

            var maxAttempts = Math.Clamp(_serverOptions.MaxPollAttempts, 1, 3600);
            var delay = TimeSpan.FromSeconds(
                Math.Clamp(_serverOptions.PollIntervalSeconds, 1, 60));

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                await Task.Delay(delay);

                var job = await context.aigenerationjobs.FirstOrDefaultAsync(
                    item => item.JobId == jobId);

                if (job == null)
                {
                    _logger.LogWarning("AI任务不存在，停止轮询。JobId={JobId}", jobId);
                    return;
                }

                if (job.Status == AIJobStatus.Cancelled)
                {
                    return;
                }

                var history = await provider.GetHistoryAsync(providerJobId);
                job.OutputJson = history.RawJson;
                job.UpdatedAt = DateTime.UtcNow;

                if (history.IsFailed)
                {
                    job.Status = AIJobStatus.Failed;
                    job.Progress = "100";
                    job.ProgressValue = 100;
                    job.ErrorCode = ErrorCodes.AiProviderError;
                    job.ErrorMessage = history.ErrorMessage ?? "ComfyUI Server 任务执行失败。";
                    job.CompletedAt = DateTime.UtcNow;
                    await context.SaveChangesAsync();
                    return;
                }

                if (history.IsCancelled)
                {
                    job.Status = AIJobStatus.Cancelled;
                    job.Progress = "100";
                    job.ProgressValue = 100;
                    job.CompletedAt = DateTime.UtcNow;
                    await context.SaveChangesAsync();
                    return;
                }

                if (!history.IsCompleted)
                {
                    job.Status = history.Status == "in_progress"
                        ? AIJobStatus.Running
                        : AIJobStatus.Queued;
                    job.ProgressValue = Math.Clamp(history.ProgressValue, 2, 95);
                    job.Progress = job.ProgressValue.ToString();
                    await context.SaveChangesAsync();
                    continue;
                }

                if (history.Outputs.Count == 0)
                {
                    job.Status = AIJobStatus.Failed;
                    job.Progress = "100";
                    job.ProgressValue = 100;
                    job.ErrorCode = ErrorCodes.AiProviderError;
                    job.ErrorMessage = "ComfyUI Server 任务已完成，但没有返回可下载的输出文件。";
                    job.CompletedAt = DateTime.UtcNow;
                    await context.SaveChangesAsync();
                    return;
                }

                job.Status = AIJobStatus.Uploading;
                job.Progress = "96";
                job.ProgressValue = 96;
                await context.SaveChangesAsync();

                var request = DeserializeRequest(job.InputJson, job.ParametersJson);
                await resultService.SaveProviderOutputsAsync(
                    job.JobId,
                    job.UserID,
                    workflow,
                    request,
                    history.Outputs);

                job.Status = AIJobStatus.Succeeded;
                job.Progress = "100";
                job.ProgressValue = 100;
                job.CompletedAt = DateTime.UtcNow;
                job.UpdatedAt = DateTime.UtcNow;
                await context.SaveChangesAsync();

                _logger.LogInformation(
                    "ComfyUI Server 任务完成。JobId={JobId}, ProviderJobId={ProviderJobId}, OutputCount={OutputCount}",
                    jobId,
                    providerJobId,
                    history.Outputs.Count);

                return;
            }

            await MarkTimeoutAsync(jobId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ComfyUI Server 任务后台处理失败。JobId={JobId}", jobId);
            await MarkFailedAsync(jobId, ex.Message);
        }
    }

    private static AIGenerationSubmitRequest DeserializeRequest(
        string? inputJson,
        string? parametersJson)
    {
        var json = !string.IsNullOrWhiteSpace(inputJson)
            ? inputJson
            : parametersJson;

        if (string.IsNullOrWhiteSpace(json))
        {
            return new AIGenerationSubmitRequest();
        }

        return JsonSerializer.Deserialize<AIGenerationSubmitRequest>(json, JsonOptions)
            ?? new AIGenerationSubmitRequest();
    }

    private async Task MarkCancelledAsync(
        AiGenerationJob job,
        CancellationToken cancellationToken)
    {
        job.Status = AIJobStatus.Cancelled;
        job.Progress = "100";
        job.ProgressValue = 100;
        job.CompletedAt = DateTime.UtcNow;
        job.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task MarkTimeoutAsync(string jobId)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DesignHubContext>();
        var job = await context.aigenerationjobs.FirstOrDefaultAsync(
            item => item.JobId == jobId);

        if (job == null || IsTerminal(job.Status))
        {
            return;
        }

        job.Status = AIJobStatus.Timeout;
        job.ErrorCode = ErrorCodes.AiJobTimeout;
        job.ErrorMessage = "ComfyUI Server 任务执行超时，请检查服务器负载后重试。";
        job.Progress = "100";
        job.ProgressValue = 100;
        job.CompletedAt = DateTime.UtcNow;
        job.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
    }

    private async Task MarkFailedAsync(string jobId, string message)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DesignHubContext>();
        var job = await context.aigenerationjobs.FirstOrDefaultAsync(
            item => item.JobId == jobId);

        if (job == null || IsTerminal(job.Status))
        {
            return;
        }

        job.Status = AIJobStatus.Failed;
        job.ErrorCode = ErrorCodes.AiProviderError;
        job.ErrorMessage = message;
        job.Progress = "100";
        job.ProgressValue = 100;
        job.CompletedAt = DateTime.UtcNow;
        job.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
    }

    private static bool IsTerminal(string status)
    {
        return status is AIJobStatus.Succeeded
            or AIJobStatus.Failed
            or AIJobStatus.Cancelled
            or AIJobStatus.Timeout;
    }
}
