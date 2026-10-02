using Altinn.Auth.AuditLog.Configuration;
using Altinn.Auth.AuditLog.Queue;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Altinn.Auth.AuditLog.Health;

/// <summary>
/// Reports a consumer as unhealthy when it has not completed a receive call within
/// <see cref="QueueConsumerSettings.HealthStaleAfter"/>. Not tagged <c>live</c>: a stuck consumer
/// takes the replica out of rotation for readiness but does not restart it.
/// </summary>
public sealed class QueueConsumerHealthCheck : IHealthCheck
{
    private readonly QueueConsumerHealthState _state;
    private readonly IOptionsMonitor<QueueConsumerSettings> _settings;
    private readonly TimeProvider _timeProvider;

    public QueueConsumerHealthCheck(
        QueueConsumerHealthState state,
        IOptionsMonitor<QueueConsumerSettings> settings,
        TimeProvider timeProvider)
    {
        _state = state;
        _settings = settings;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc/>
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var snapshot = _state.Snapshot();
        if (snapshot.Count == 0)
        {
            return Task.FromResult(HealthCheckResult.Healthy("No queue consumers registered"));
        }

        var now = _timeProvider.GetUtcNow();
        var staleAfter = _settings.CurrentValue.HealthStaleAfter;
        var data = new Dictionary<string, object>(snapshot.Count);
        var stale = new List<string>();

        foreach (var (queue, lastReceive) in snapshot)
        {
            var age = now - lastReceive;
            data[queue] = age;
            if (age > staleAfter)
            {
                stale.Add(queue);
            }
        }

        if (stale.Count > 0)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                $"Queue consumer(s) have not received for more than {staleAfter}: {string.Join(", ", stale)}",
                data: data));
        }

        return Task.FromResult(HealthCheckResult.Healthy("All queue consumers are receiving", data));
    }
}
