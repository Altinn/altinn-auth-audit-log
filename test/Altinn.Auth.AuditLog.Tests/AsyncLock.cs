namespace Altinn.Auth.AuditLog.Tests;

internal sealed class AsyncLock()
: AsyncConcurrencyLimiter(1)
{
}
