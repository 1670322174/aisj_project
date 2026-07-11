// 作用：定义 AI 任务查询与状态更新接口。
// 真实任务只由 IAIGenerationService 创建，本接口不再提供占位任务创建能力。

using InteriorDesignWeb.Application.Common;
using InteriorDesignWeb.Models.DTOs.AI;

namespace InteriorDesignWeb.Services.AI;

public interface IAIJobService
{
    Task<AIJobDto?> GetJobAsync(
        string jobId,
        int? userId = null,
        CancellationToken cancellationToken = default);

    Task<PagedResult<AIJobDto>> GetUserJobsAsync(
        int userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AIJobResultDto>> GetJobResultsAsync(
        string jobId,
        int userId,
        CancellationToken cancellationToken = default);

    Task DeleteJobAsync(
        string jobId,
        int userId,
        CancellationToken cancellationToken = default);

    Task MarkProcessingAsync(
        string jobId,
        string? providerJobId = null,
        CancellationToken cancellationToken = default);

    Task MarkSucceededAsync(
        string jobId,
        string? outputJson = null,
        CancellationToken cancellationToken = default);

    Task MarkFailedAsync(
        string jobId,
        string errorCode,
        string errorMessage,
        CancellationToken cancellationToken = default);
}
