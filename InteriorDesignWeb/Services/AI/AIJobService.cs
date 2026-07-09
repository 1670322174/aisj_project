// 作用：实现 AI 生成任务中心的核心业务逻辑。
// 当前阶段只管理任务生命周期，不直接接入 ComfyUI，也不处理 7 个具体工作流。

using System.Text.Json;
using System.Text.Json.Serialization;
using InteriorDesignWeb.Application.Common;
using InteriorDesignWeb.Models.DTOs.AI;
using InteriorDesignWeb.Models.Entities;
using InteriorDesignWeb.Repositories.AI;
using Microsoft.AspNetCore.Http;

namespace InteriorDesignWeb.Services.AI;

public class AIJobService : IAIJobService
{
    private readonly IAIJobRepository _repository;
    private readonly ILogger<AIJobService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public AIJobService(
        IAIJobRepository repository,
        ILogger<AIJobService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<AIJobDto> CreateJobAsync(
        CreateAIJobRequest request,
        int? userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.WorkflowCode))
        {
            throw AppException.Validation("WorkflowCode 不能为空。");
        }

        var jobId = Guid.NewGuid().ToString("N");

        // 当前阶段还没有提交到真实模型，所以 PromptId 先使用本地 JobId 占位。
        // 后续接入 ComfyUI 后，ProviderJobId / PromptId 会更新为真实的 ComfyUI prompt_id。
        var inputJson = JsonSerializer.Serialize(request, JsonOptions);
        var parametersJson = JsonSerializer.Serialize(
            request.Parameters ?? new Dictionary<string, object?>(),
            JsonOptions);

        var job = new AiGenerationJob
        {
            JobId = jobId,
            PromptId = jobId,
            ProviderJobId = null,
            UserID = userId,
            Status = AIJobStatus.Created,
            WorkflowCode = request.WorkflowCode,
            ModelCode = request.ModelCode,
            ProviderType = string.IsNullOrWhiteSpace(request.ProviderType)
                ? "ComfyUI"
                : request.ProviderType,
            Prompt = request.Prompt,
            NegativePrompt = request.NegativePrompt,
            ParametersJson = parametersJson,
            InputJson = inputJson,
            Progress = "0",
            ProgressValue = 0,
            CostUnits = request.CostUnits <= 0 ? 1 : request.CostUnits,
            CreatedAt = DateTime.UtcNow
        };

        await _repository.AddAsync(job, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "AI任务已创建。JobId={JobId}, UserID={UserID}, WorkflowCode={WorkflowCode}",
            job.JobId,
            job.UserID,
            job.WorkflowCode);

        return ToDto(job);
    }

    public async Task<AIJobDto?> GetJobAsync(
        string jobId,
        int? userId = null,
        CancellationToken cancellationToken = default)
    {
        var job = await _repository.GetByIdAsync(jobId, userId, cancellationToken);
        return job == null ? null : ToDto(job);
    }

    public async Task<PagedResult<AIJobDto>> GetUserJobsAsync(
        int userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var jobs = await _repository.GetUserJobsAsync(
            userId,
            page,
            pageSize,
            cancellationToken);

        var total = await _repository.CountUserJobsAsync(
            userId,
            cancellationToken);

        return PagedResult<AIJobDto>.Create(
            jobs.Select(ToDto).ToList(),
            page,
            pageSize,
            total);
    }


    public async Task<IReadOnlyList<AIJobResultDto>> GetJobResultsAsync(
        string jobId,
        int userId,
        CancellationToken cancellationToken = default)
    {
        var job = await _repository.GetByIdAsync(jobId, userId, cancellationToken);
        if (job == null)
        {
            throw new AppException(
                ErrorCodes.AiJobNotFound,
                "AI任务不存在或无权访问。",
                StatusCodes.Status404NotFound);
        }

        var images = await _repository.GetJobImagesAsync(jobId, userId, cancellationToken);
        return images.Select(image => new AIJobResultDto
        {
            AiImageID = image.AiImageID,
            JobId = image.JobId,
            ImageUrl = image.ImageUrl,
            CosPath = image.CosPath,
            ThumbnailPath = image.ThumbnailPath,
            SourceType = image.SourceType,
            MetadataJson = image.MetadataJson,
            CreatedAt = image.CreatedAt
        }).ToList();
    }

    public async Task MarkProcessingAsync(
        string jobId,
        string? providerJobId = null,
        CancellationToken cancellationToken = default)
    {
        var job = await GetRequiredJobAsync(jobId, cancellationToken);

        job.Status = AIJobStatus.Processing;
        job.ProviderJobId = providerJobId ?? job.ProviderJobId;
        job.PromptId = providerJobId ?? job.PromptId;
        job.Progress = "1";
        job.ProgressValue = 1;
        job.StartedAt ??= DateTime.UtcNow;
        job.UpdatedAt = DateTime.UtcNow;

        _repository.Update(job);
        await _repository.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkSucceededAsync(
        string jobId,
        string? outputJson = null,
        CancellationToken cancellationToken = default)
    {
        var job = await GetRequiredJobAsync(jobId, cancellationToken);

        job.Status = AIJobStatus.Succeeded;
        job.OutputJson = outputJson ?? job.OutputJson;
        job.Progress = "100";
        job.ProgressValue = 100;
        job.CompletedAt = DateTime.UtcNow;
        job.UpdatedAt = DateTime.UtcNow;

        _repository.Update(job);
        await _repository.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkFailedAsync(
        string jobId,
        string errorCode,
        string errorMessage,
        CancellationToken cancellationToken = default)
    {
        var job = await GetRequiredJobAsync(jobId, cancellationToken);

        job.Status = AIJobStatus.Failed;
        job.ErrorCode = errorCode;
        job.ErrorMessage = errorMessage;
        job.CompletedAt = DateTime.UtcNow;
        job.UpdatedAt = DateTime.UtcNow;

        _repository.Update(job);
        await _repository.SaveChangesAsync(cancellationToken);

        _logger.LogWarning(
            "AI任务失败。JobId={JobId}, ErrorCode={ErrorCode}, ErrorMessage={ErrorMessage}",
            jobId,
            errorCode,
            errorMessage);
    }

    private async Task<AiGenerationJob> GetRequiredJobAsync(
        string jobId,
        CancellationToken cancellationToken)
    {
        var job = await _repository.GetByIdAsync(jobId, null, cancellationToken);

        if (job == null)
        {
            throw new AppException(
                ErrorCodes.AiJobNotFound,
                "AI任务不存在。",
                StatusCodes.Status404NotFound);
        }

        return job;
    }

    private static AIJobDto ToDto(AiGenerationJob job)
    {
        return new AIJobDto
        {
            JobId = job.JobId,
            Status = job.Status,
            UserID = job.UserID,
            WorkflowCode = job.WorkflowCode,
            ModelCode = job.ModelCode,
            ProviderType = job.ProviderType,
            Prompt = job.Prompt,
            NegativePrompt = job.NegativePrompt,
            ProgressValue = job.ProgressValue,
            CostUnits = job.CostUnits,
            ErrorCode = job.ErrorCode,
            ErrorMessage = job.ErrorMessage,
            CreatedAt = job.CreatedAt,
            StartedAt = job.StartedAt,
            CompletedAt = job.CompletedAt,
            UpdatedAt = job.UpdatedAt,
            ImageCount = job.Images?.Count ?? 0
        };
    }
}
