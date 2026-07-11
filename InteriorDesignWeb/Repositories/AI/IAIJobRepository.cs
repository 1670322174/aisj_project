// 作用：定义 AI 生成任务的数据访问接口。
// 当前阶段只负责 aigenerationjobs 表的基础查询、新增、更新，不处理具体工作流逻辑。

using InteriorDesignWeb.Models.Entities;

namespace InteriorDesignWeb.Repositories.AI;

public interface IAIJobRepository
{
    Task<AiGenerationJob?> GetByIdAsync(
        string jobId,
        int? userId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AiGenerationJob>> GetUserJobsAsync(
        int userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AiGenerationJobImage>> GetJobImagesAsync(
        string jobId,
        int userId,
        CancellationToken cancellationToken = default);

    Task<long> CountUserJobsAsync(
        int userId,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        AiGenerationJob job,
        CancellationToken cancellationToken = default);

    void Update(AiGenerationJob job);

    Task HardDeleteAsync(
        AiGenerationJob job,
        int ownerUserId,
        DateTime detachedAt,
        DateTime cleanupEligibleAt,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
