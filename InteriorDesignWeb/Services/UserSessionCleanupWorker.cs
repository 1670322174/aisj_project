using InteriorDesignWeb.Data;
using Microsoft.EntityFrameworkCore;

namespace InteriorDesignWeb.Services;

public sealed class UserSessionCleanupWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<UserSessionCleanupWorker> _logger;

    public UserSessionCleanupWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<UserSessionCleanupWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<DesignHubContext>();
                var cutoff = DateTime.UtcNow.AddDays(-7);
                var deleted = await context.usersessions
                    .Where(session => session.ExpiresAt < cutoff
                        || (session.RevokedAt != null && session.RevokedAt < cutoff))
                    .ExecuteDeleteAsync(stoppingToken);

                if (deleted > 0)
                {
                    _logger.LogInformation("Removed {Count} expired user sessions", deleted);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "User session cleanup failed");
            }

            await Task.Delay(TimeSpan.FromDays(1), stoppingToken);
        }
    }
}
