using Npgsql;
using System.Net.Sockets;

namespace Altinn.Auth.AuditLog.Queue;

/// <summary>
/// Decides whether a persistence failure is worth retrying (transient) or will fail the same way every
/// time for the same input (permanent). The distinction drives the retry design: transient failures keep
/// the messages on the queue, permanent failures isolate and poison the offending message.
/// </summary>
internal static class TransientErrorClassifier
{
    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="exception"/> is transient.
    /// </summary>
    public static bool IsTransient(Exception exception)
    {
        return exception switch
        {
            // Postgres errors carry a SQLSTATE; classify by class.
            PostgresException pg => IsTransientSqlState(pg),

            // Npgsql's own classification covers broken connections, pool exhaustion and command timeouts.
            NpgsqlException npgsql => npgsql.IsTransient || (npgsql.InnerException is { } inner && IsTransient(inner)),

            TimeoutException => true,
            IOException => true,
            SocketException => true,

            // A cancellation that is not ours (e.g. a timed-out command) is treated as transient by the caller;
            // the caller checks its own token before asking us.
            OperationCanceledException => true,

            AggregateException aggregate => aggregate.InnerExceptions.Count > 0 && aggregate.InnerExceptions.All(IsTransient),

            _ => false,
        };
    }

    private static bool IsTransientSqlState(PostgresException exception)
    {
        var sqlState = exception.SqlState;
        if (string.IsNullOrEmpty(sqlState) || sqlState.Length < 2)
        {
            return exception.IsTransient;
        }

        // "no partition of relation ... found for row" is reported as a check violation (23514), but it is
        // the partition creation job that is behind, not the data. Never poison for this.
        if (sqlState == PostgresErrorCodes.CheckViolation && exception.MessageText.Contains("no partition of relation", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return sqlState switch
        {
            PostgresErrorCodes.SerializationFailure => true,     // 40001
            PostgresErrorCodes.DeadlockDetected => true,         // 40P01
            PostgresErrorCodes.LockNotAvailable => true,         // 55P03
            PostgresErrorCodes.ObjectInUse => true,              // 55006
            PostgresErrorCodes.TooManyConnections => true,       // 53300
            _ => sqlState[..2] switch
            {
                "08" => true, // connection exception
                "53" => true, // insufficient resources
                "57" => true, // operator intervention (shutdown, cancel)
                "58" => true, // system error (I/O)
                "XX" => true, // internal error
                _ => exception.IsTransient,
            },
        };
    }
}
