// 作用：实现 AI 生成任务的数据访问逻辑。
// 当前阶段只封装 EF Core 对 aigenerationjobs / aigenerationjobimages 的基础操作。

using InteriorDesignWeb.Data;
using InteriorDesignWeb.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace InteriorDesignWeb.Repositories.AI;

public class AIJobRepository : IAIJobRepository
{
    private readonly DesignHubContext _context;

    public AIJobRepository(DesignHubContext context)
    {
        _context = context;
    }

    public async Task<AiGenerationJob?> GetByIdAsync(
        string jobId,
        int? userId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.aigenerationjobs
            .Include(job => job.Images)
            .AsQueryable();

        // 如果传入 userId，只允许查询当前用户自己的任务。
        if (userId.HasValue)
        {
            query = query.Where(job => job.UserID == userId.Value);
        }

        return await query.FirstOrDefaultAsync(
            job => job.JobId == jobId,
            cancellationToken);
    }

    public async Task<IReadOnlyList<AiGenerationJob>> GetUserJobsAsync(
        int userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        page = page <= 0 ? 1 : page;
        pageSize = pageSize <= 0 ? 20 : pageSize;
        pageSize = pageSize > 100 ? 100 : pageSize;

        return await _context.aigenerationjobs
            .Include(job => job.Images)
            .Where(job => job.UserID == userId)
            .OrderByDescending(job => job.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<long> CountUserJobsAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        return await _context.aigenerationjobs
            .LongCountAsync(job => job.UserID == userId, cancellationToken);
    }

    public async Task AddAsync(
        AiGenerationJob job,
        CancellationToken cancellationToken = default)
    {
        await _context.aigenerationjobs.AddAsync(job, cancellationToken);
    }

    public void Update(AiGenerationJob job)
    {
        _context.aigenerationjobs.Update(job);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
