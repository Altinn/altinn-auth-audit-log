namespace Altinn.Auth.AuditLog.Queue;

/// <summary>
/// Minimal circuit breaker for one consumer. After a number of consecutive transient/systemic batch failures the
/// circuit opens and receiving pauses, so a database outage does not burn through the dequeue count of the whole
/// backlog.
/// </summary>
internal sealed class CircuitBreaker
{
    private readonly Lock _lock = new();
    private readonly int _failuresBeforeOpen;
    private readonly TimeSpan _openDuration;
    private readonly TimeProvider _timeProvider;

    private int _consecutiveFailures;
    private DateTimeOffset _openUntil = DateTimeOffset.MinValue;

    public CircuitBreaker(int failuresBeforeOpen, TimeSpan openDuration, TimeProvider timeProvider)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(failuresBeforeOpen, 1);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(openDuration, TimeSpan.Zero);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _failuresBeforeOpen = failuresBeforeOpen;
        _openDuration = openDuration;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Gets the number of consecutive failures recorded since the last success.
    /// </summary>
    public int ConsecutiveFailures
    {
        get
        {
            lock (_lock)
            {
                return _consecutiveFailures;
            }
        }
    }

    /// <summary>
    /// Returns <see langword="true"/> and the remaining open time if the circuit is currently open.
    /// </summary>
    public bool IsOpen(out TimeSpan remaining)
    {
        var now = _timeProvider.GetUtcNow();
        DateTimeOffset openUntil;

        lock (_lock)
        {
            openUntil = _openUntil;
        }

        if (now < openUntil)
        {
            remaining = openUntil - now;
            return true;
        }

        remaining = TimeSpan.Zero;
        return false;
    }

    /// <summary>
    /// Records a successful batch; closes the circuit and resets the failure counter.
    /// </summary>
    public void RecordSuccess()
    {
        lock (_lock)
        {
            _consecutiveFailures = 0;
            _openUntil = DateTimeOffset.MinValue;
        }
    }

    /// <summary>
    /// Records a failed batch. Returns <see langword="true"/> if this failure opened the circuit.
    /// </summary>
    public bool RecordFailure()
    {
        var openUntil = _timeProvider.GetUtcNow() + _openDuration;

        lock (_lock)
        {
            if (++_consecutiveFailures < _failuresBeforeOpen)
            {
                return false;
            }

            _openUntil = openUntil;
            _consecutiveFailures = 0;
            return true;
        }
    }
}
