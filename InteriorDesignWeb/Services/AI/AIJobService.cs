// 作用：实现 AI 生成任务中心的核心业务逻辑。
// 任务创建由 AIGenerationService 负责，本服务只管理查询与通用状态更新。

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

    public AIJobService(
        IAIJobRepository repository,
        ILogger<AIJobService> logger)
    {
        _repository = repository;
        _logger = logger;
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
            JobId = image.JobId ?? jobId,
            ImageUrl = image.ImageUrl,
            CosPath = image.CosPath,
            ThumbnailPath = image.ThumbnailPath,
            SourceType = image.SourceType,
            MetadataJson = image.MetadataJson,
            CreatedAt = image.CreatedAt
        }).ToList();
    }

    public async Task DeleteJobAsync(
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

        var canDelete = job.Status is AIJobStatus.Succeeded
            or AIJobStatus.Failed
            or AIJobStatus.Cancelled
            or AIJobStatus.Timeout;
        if (!canDelete)
        {
            throw new AppException(
                ErrorCodes.Conflict,
                "运行中的任务不能删除，请先取消任务或等待任务结束。",
                StatusCodes.Status409Conflict);
        }

        var detachedAt = DateTime.UtcNow;
        await _repository.HardDeleteAsync(
            job,
            userId,
            detachedAt,
            detachedAt.AddDays(7),
            cancellationToken);

        _logger.LogInformation(
            "用户永久删除AI任务条目。图片资产已解除任务关联。UserID={UserID}, JobId={JobId}, ImageCount={ImageCount}",
            userId,
            jobId,
            job.Images.Count);
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
            ProviderJobId = job.ProviderJobId,
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
