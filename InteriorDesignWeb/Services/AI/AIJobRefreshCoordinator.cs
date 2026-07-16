using System.Collections.Concurrent;

namespace InteriorDesignWeb.Services.AI;

/// <summary>Prevents WebSocket completion and the recovery poller from finalizing the same job concurrently.</summary>
public sealed class AIJobRefreshCoordinator
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    public async Task RunAsync(string jobId, Func<Task> action, CancellationToken cancellationToken)
    {
        var gate = _locks.GetOrAdd(jobId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try { await action(); }
        finally { gate.Release(); }
    }
}
