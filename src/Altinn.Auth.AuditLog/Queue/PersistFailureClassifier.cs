using Npgsql;
using System.Net.Sockets;

namespace Altinn.Auth.AuditLog.Queue;

/// <summary>
/// How a persistence failure should be handled.
/// </summary>
public enum PersistFailureClass
{
    /// <summary>
    /// Will likely succeed if retried (connection, timeout, resources, missing partition). Retry with backoff,
    /// then leave the messages on the queue.
    /// </summary>
    Transient,

    /// <summary>
    /// The data in a specific message is rejected by the database (constraint, invalid value). The message
    /// can never be stored and is isolated and poisoned.
    /// </summary>
    DataError,

    /// <summary>
    /// The system is misconfigured (schema mismatch, insufficient privilege, programming error). Retrying the
    /// same message will fail, but so will every other message, so nothing is poisoned: the messages stay on
    /// the queue and the circuit breaker pauses consumption until someone fixes the system.
    /// </summary>
    Systemic,
}

/// <summary>
/// Classifies persistence failures. The distinction drives the retry design: transient failures keep the
/// messages on the queue, data errors isolate and poison the offending message, and systemic failures pause
/// consumption without poisoning anything. Unknown exceptions are treated as systemic, never as data errors,
/// because poisoning is the only outcome that removes audit evidence from the source queue.
/// </summary>
internal static class PersistFailureClassifier
{
    /// <summary>
    /// Classifies <paramref name="exception"/>.
    /// </summary>
    public static PersistFailureClass Classify(Exception exception)
    {
        return exception switch
        {
            // Postgres errors carry a SQLSTATE; classify by class.
            PostgresException pg => ClassifySqlState(pg),

            // Npgsql's own classification covers broken connections, pool exhaustion and command timeouts.
            NpgsqlException npgsql when npgsql.IsTransient => PersistFailureClass.Transient,
            NpgsqlException { InnerException: { } inner } => Classify(inner),
            NpgsqlException => PersistFailureClass.Systemic,

            TimeoutException => PersistFailureClass.Transient,
            IOException => PersistFailureClass.Transient,
            SocketException => PersistFailureClass.Transient,

            // A cancellation that is not ours (e.g. a timed-out command); the caller checks its own token first.
            OperationCanceledException => PersistFailureClass.Transient,

            // Repository guards (missing created/operation/decision, context not an object) reject the event itself.
            ArgumentException => PersistFailureClass.DataError,

            AggregateException { InnerExceptions.Count: > 0 } aggregate => ClassifyAggregate(aggregate),

            _ => PersistFailureClass.Systemic,
        };
    }

    /// <summary>
    /// Convenience for callers that only care about retrying.
    /// </summary>
    public static bool IsTransient(Exception exception)
        => Classify(exception) == PersistFailureClass.Transient;

    private static PersistFailureClass ClassifyAggregate(AggregateException aggregate)
    {
        var worst = PersistFailureClass.Transient;
        foreach (var inner in aggregate.InnerExceptions)
        {
            var cls = Classify(inner);
            if (cls == PersistFailureClass.Systemic)
            {
                return PersistFailureClass.Systemic;
            }

            if (cls == PersistFailureClass.DataError)
            {
                worst = PersistFailureClass.DataError;
            }
        }

        return worst;
    }

    private static PersistFailureClass ClassifySqlState(PostgresException exception)
    {
        var sqlState = exception.SqlState;
        if (string.IsNullOrEmpty(sqlState) || sqlState.Length < 2)
        {
            return exception.IsTransient ? PersistFailureClass.Transient : PersistFailureClass.Systemic;
        }

        // "no partition of relation ... found for row" is reported as a check violation (23514), but it is
        // the partition creation job that is behind, not the data. Never poison for this.
        if (sqlState == PostgresErrorCodes.CheckViolation && exception.MessageText.Contains("no partition of relation", StringComparison.OrdinalIgnoreCase))
        {
            return PersistFailureClass.Transient;
        }

        switch (sqlState)
        {
            case PostgresErrorCodes.SerializationFailure: // 40001
            case PostgresErrorCodes.DeadlockDetected:     // 40P01
            case PostgresErrorCodes.LockNotAvailable:     // 55P03
            case PostgresErrorCodes.ObjectInUse:          // 55006
            case PostgresErrorCodes.TooManyConnections:   // 53300
                return PersistFailureClass.Transient;
        }

        return sqlState[..2] switch
        {
            "08" => PersistFailureClass.Transient, // connection exception
            "53" => PersistFailureClass.Transient, // insufficient resources
            "57" => PersistFailureClass.Transient, // operator intervention (shutdown, cancel)
            "58" => PersistFailureClass.Transient, // system error (I/O)
            "XX" => PersistFailureClass.Transient, // internal error

            "22" => PersistFailureClass.DataError, // data exception (invalid value, out of range, bad text representation)
            "23" => PersistFailureClass.DataError, // integrity constraint violation (not null, FK, check, unique)

            // Everything else (42 syntax/access rule: undefined column, insufficient privilege; 3F schema; 28 auth;
            // 0A unsupported feature; 54 program limit; ...) is a system problem, not a message problem.
            _ => exception.IsTransient ? PersistFailureClass.Transient : PersistFailureClass.Systemic,
        };
    }
}
