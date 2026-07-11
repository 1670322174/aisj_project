using InteriorDesignWeb.Data;
using InteriorDesignWeb.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace InteriorDesignWeb.Services.AI;

/// <summary>
/// Refreshes non-terminal AI jobs independently of browser requests. Jobs are
/// read from the database on every pass, so processing resumes after a restart.
/// </summary>
public sealed class AIJobBackgroundWorker : BackgroundService
{
    private static readonly string[] ActiveStatuses =
    [
        AIJobStatus.Created,
        AIJobStatus.Queued,
        AIJobStatus.Running,
        AIJobStatus.Processing,
        AIJobStatus.Uploading
    ];

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AIJobBackgroundWorker> _logger;

    public AIJobBackgroundWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<AIJobBackgroundWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RefreshJobsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AI job background pass failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    private async Task RefreshJobsAsync(CancellationToken cancellationToken)
    {
        using var lookupScope = _scopeFactory.CreateScope();
        var context = lookupScope.ServiceProvider.GetRequiredService<DesignHubContext>();
        var jobs = await context.aigenerationjobs
            .AsNoTracking()
            .Where(job => !job.IsDeleted
                && job.UserID != null
                && job.ProviderJobId != null
                && ActiveStatuses.Contains(job.Status))
            .OrderBy(job => job.UpdatedAt ?? job.CreatedAt)
            .Select(job => new { job.JobId, UserId = job.UserID!.Value })
            .Take(20)
            .ToListAsync(cancellationToken);

        foreach (var job in jobs)
        {
            try
            {
                using var jobScope = _scopeFactory.CreateScope();
                var generationService = jobScope.ServiceProvider
                    .GetRequiredService<IAIGenerationService>();
                await generationService.RefreshAsync(
                    job.JobId,
                    job.UserId,
                    cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "AI job refresh failed. JobId={JobId}",
                    job.JobId);
            }
        }
    }
}
