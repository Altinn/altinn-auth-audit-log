namespace Altinn.Auth.AuditLog.Queue;

/// <summary>
/// Minimal circuit breaker shared by all worker loops of one consumer. After a number of consecutive
/// transient batch failures the circuit opens and receiving pauses, so a database outage does not
/// burn through the dequeue count of the whole backlog.
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
        _failuresBeforeOpen = Math.Max(1, failuresBeforeOpen);
        _openDuration = openDuration;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Gets the number of consecutive transient failures recorded since the last success.
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
        lock (_lock)
        {
            var now = _timeProvider.GetUtcNow();
            if (now < _openUntil)
            {
                remaining = _openUntil - now;
                return true;
            }

            remaining = TimeSpan.Zero;
            return false;
        }
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
    /// Records a transient batch failure. Returns <see langword="true"/> if this failure opened the circuit.
    /// </summary>
    public bool RecordFailure()
    {
        lock (_lock)
        {
            _consecutiveFailures++;
            if (_consecutiveFailures >= _failuresBeforeOpen)
            {
                _openUntil = _timeProvider.GetUtcNow() + _openDuration;
                _consecutiveFailures = 0;
                return true;
            }

            return false;
        }
    }
}
