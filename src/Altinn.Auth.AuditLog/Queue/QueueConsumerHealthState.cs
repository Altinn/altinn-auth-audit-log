using System.Collections.Concurrent;

namespace Altinn.Auth.AuditLog.Queue;

/// <summary>
/// Tracks liveness of each queue consumer (last successful receive call) for the health check.
/// </summary>
public sealed class QueueConsumerHealthState
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastSuccessfulReceive = new();

    /// <summary>
    /// Registers a consumer; it is considered healthy as of <paramref name="now"/> until proven otherwise.
    /// </summary>
    public void Register(string queue, DateTimeOffset now)
        => _lastSuccessfulReceive[queue] = now;

    /// <summary>
    /// Records a successful receive call (empty results count: the consumer is alive).
    /// </summary>
    public void ReportReceive(string queue, DateTimeOffset now)
        => _lastSuccessfulReceive[queue] = now;

    /// <summary>
    /// Gets a snapshot of the last successful receive per registered queue.
    /// </summary>
    public IReadOnlyDictionary<string, DateTimeOffset> Snapshot()
        => new Dictionary<string, DateTimeOffset>(_lastSuccessfulReceive);
}
