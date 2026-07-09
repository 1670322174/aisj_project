// 作用：定义 AI 生成任务中心的业务接口。
// 当前阶段只提供任务创建、查询、状态更新能力，不负责调用具体生图模型。

using InteriorDesignWeb.Application.Common;
using InteriorDesignWeb.Models.DTOs.AI;

namespace InteriorDesignWeb.Services.AI;

public interface IAIJobService
{
    Task<AIJobDto> CreateJobAsync(
        CreateAIJobRequest request,
        int? userId,
        CancellationToken cancellationToken = default);

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
