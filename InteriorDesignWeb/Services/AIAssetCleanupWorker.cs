using InteriorDesignWeb.Data;
using InteriorDesignWeb.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace InteriorDesignWeb.Services;

/// <summary>
/// Permanently removes AI assets only after their retention delay and only
/// when no project relation or cover still points to the image.
/// </summary>
public sealed class AIAssetCleanupWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AIAssetCleanupWorker> _logger;

    public AIAssetCleanupWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<AIAssetCleanupWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Cleanup is deliberately infrequent; deletion has a seven-day grace period.
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AI asset cleanup pass failed");
            }

            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }

    private async Task CleanupBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DesignHubContext>();
        var cos = scope.ServiceProvider.GetRequiredService<CosService>();
        var now = DateTime.UtcNow;

        var candidates = await context.aigenerationjobimages
            .Where(image => image.RetentionStatus == AiGenerationJobImage.RetentionCleanupPending
                && image.CleanupEligibleAt != null
                && image.CleanupEligibleAt <= now)
            .OrderBy(image => image.CleanupEligibleAt)
            .Take(50)
            .ToListAsync(cancellationToken);

        foreach (var image in candidates)
        {
            var referenced = await context.projectimages
                    .AnyAsync(link => link.AiImageID == image.AiImageID, cancellationToken)
                || await context.projects
                    .AnyAsync(project => project.CoverAiImageID == image.AiImageID, cancellationToken);

            if (referenced)
            {
                image.RetentionStatus = AiGenerationJobImage.RetentionRetained;
                image.CleanupEligibleAt = null;
                continue;
            }

            try
            {
                var keys = new[] { image.CosPath, image.ThumbnailPath }
                    .Where(key => !string.IsNullOrWhiteSpace(key))
                    .Select(key => key!)
                    .Distinct(StringComparer.OrdinalIgnoreCase);

                foreach (var key in keys)
                {
                    cos.DeleteAIObject(key);
                }

                context.aigenerationjobimages.Remove(image);
                await context.SaveChangesAsync(cancellationToken);
                _logger.LogInformation(
                    "Deleted unreferenced AI asset. AiImageId={AiImageId}",
                    image.AiImageID);
            }
            catch (Exception ex)
            {
                // Keep the row for a later retry; never lose the cleanup pointer.
                _logger.LogWarning(
                    ex,
                    "Unable to delete AI asset; it will be retried. AiImageId={AiImageId}",
                    image.AiImageID);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
