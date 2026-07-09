// 作用：统一编排 AI 生图流程。
// 流程：校验工作流 -> 构建 ComfyUI workflow JSON -> 创建 AIJob -> 提交 Provider -> 后台轮询 -> 上传 COS -> 写入结果表。

using System.Text.Json;
using System.Text.Json.Serialization;
using InteriorDesignWeb.Application.Common;
using InteriorDesignWeb.Data;
using InteriorDesignWeb.Models.DTOs.AI;
using InteriorDesignWeb.Models.Entities;
using InteriorDesignWeb.Providers.AI;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;

namespace InteriorDesignWeb.Services.AI;

public class AIGenerationService : IAIGenerationService
{
    private const int MaxHistoryAttempts = 120;
    private static readonly TimeSpan HistoryDelay = TimeSpan.FromSeconds(3);

    private readonly IWorkflowRegistry _workflowRegistry;
    private readonly IWorkflowBuilder _workflowBuilder;
    private readonly IAIProvider _provider;
    private readonly DesignHubContext _context;
    private readonly IServiceScopeFactory _scopeFactory;
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
        IServiceScopeFactory scopeFactory,
        ILogger<AIGenerationService> logger)
    {
        _workflowRegistry = workflowRegistry;
        _workflowBuilder = workflowBuilder;
        _provider = provider;
        _context = context;
        _scopeFactory = scopeFactory;
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
            throw AppException.Validation("fieldName 不能为空，例如 sourceImage、referenceImage、firstFrame、lastFrame。");
        }

        var result = await _provider.UploadImageAsync(file, subfolder, overwrite, cancellationToken);
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
            ProviderType = workflow.ProviderType,
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
            job.Status = AIJobStatus.Running;
            job.Progress = "5";
            job.ProgressValue = 5;
            job.StartedAt = DateTime.UtcNow;
            job.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);

            // 后台轮询必须创建新作用域，避免请求结束后 DbContext 被释放。
            _ = Task.Run(
                () => MonitorAndSaveResultAsync(job.JobId, submitResult.ProviderJobId, workflow.WorkflowCode),
                CancellationToken.None);

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
                "AI 任务提交失败。JobId={JobId}, WorkflowCode={WorkflowCode}",
                job.JobId,
                workflow.WorkflowCode);

            throw;
        }
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

        if (string.IsNullOrWhiteSpace(job.ProviderJobId))
        {
            job.Status = AIJobStatus.Cancelled;
            job.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }

        var cancelled = await _provider.CancelAsync(job.ProviderJobId, cancellationToken);
        if (cancelled)
        {
            job.Status = AIJobStatus.Cancelled;
            job.CompletedAt = DateTime.UtcNow;
            job.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
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
            var scopedContext = scope.ServiceProvider.GetRequiredService<DesignHubContext>();
            var scopedProvider = scope.ServiceProvider.GetRequiredService<IAIProvider>();
            var resultService = scope.ServiceProvider.GetRequiredService<IAIResultService>();
            var workflowRegistry = scope.ServiceProvider.GetRequiredService<IWorkflowRegistry>();
            var workflow = workflowRegistry.GetRequired(workflowCode);

            for (var attempt = 1; attempt <= MaxHistoryAttempts; attempt++)
            {
                await Task.Delay(HistoryDelay);

                var job = await scopedContext.aigenerationjobs.FirstOrDefaultAsync(item => item.JobId == jobId);
                if (job == null)
                {
                    _logger.LogWarning("AI任务不存在，停止轮询。JobId={JobId}", jobId);
                    return;
                }

                if (job.Status == AIJobStatus.Cancelled)
                {
                    _logger.LogInformation("AI任务已取消，停止轮询。JobId={JobId}", jobId);
                    return;
                }

                var history = await scopedProvider.GetHistoryAsync(providerJobId);
                if (!history.IsCompleted)
                {
                    job.Status = AIJobStatus.Running;
                    job.ProgressValue = Math.Min(95, 5 + attempt);
                    job.Progress = job.ProgressValue.ToString();
                    job.UpdatedAt = DateTime.UtcNow;
                    await scopedContext.SaveChangesAsync();
                    continue;
                }

                job.Status = AIJobStatus.Uploading;
                job.Progress = "96";
                job.ProgressValue = 96;
                job.OutputJson = history.RawJson;
                job.UpdatedAt = DateTime.UtcNow;
                await scopedContext.SaveChangesAsync();

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
                await scopedContext.SaveChangesAsync();

                _logger.LogInformation(
                    "AI任务完成。JobId={JobId}, ProviderJobId={ProviderJobId}, OutputCount={OutputCount}",
                    jobId,
                    providerJobId,
                    history.Outputs.Count);

                return;
            }

            await MarkTimeoutAsync(jobId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI任务后台处理失败。JobId={JobId}", jobId);
            await MarkFailedAsync(jobId, ex.Message);
        }
    }

    private static AIGenerationSubmitRequest DeserializeRequest(string? inputJson, string? parametersJson)
    {
        var json = !string.IsNullOrWhiteSpace(inputJson) ? inputJson : parametersJson;
        if (string.IsNullOrWhiteSpace(json))
        {
            return new AIGenerationSubmitRequest();
        }

        return JsonSerializer.Deserialize<AIGenerationSubmitRequest>(json, JsonOptions)
            ?? new AIGenerationSubmitRequest();
    }

    private async Task MarkTimeoutAsync(string jobId)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DesignHubContext>();
        var job = await context.aigenerationjobs.FirstOrDefaultAsync(item => item.JobId == jobId);
        if (job == null)
        {
            return;
        }

        job.Status = AIJobStatus.Timeout;
        job.ErrorCode = ErrorCodes.AiJobTimeout;
        job.ErrorMessage = "AI任务执行超时，请稍后重试。";
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
        var job = await context.aigenerationjobs.FirstOrDefaultAsync(item => item.JobId == jobId);
        if (job == null)
        {
            return;
        }

        job.Status = AIJobStatus.Failed;
        job.ErrorCode = ErrorCodes.AiProviderError;
        job.ErrorMessage = message;
        job.CompletedAt = DateTime.UtcNow;
        job.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
    }
}
