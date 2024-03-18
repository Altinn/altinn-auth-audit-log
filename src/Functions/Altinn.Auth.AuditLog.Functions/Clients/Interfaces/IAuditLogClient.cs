using Altinn.Auth.AuditLog.Core.Models;

namespace Altinn.Auth.AuditLog.Functions.Clients.Interfaces
{
    public interface IAuditLogClient
    {
        /// <summary>
        /// Posts an authentication event to the auditlog api.
        /// </summary>
        /// <param name="authEvent">The authevent to be created</param>
        Task SaveAuthenticationEvent(AuthenticationEvent authEvent);

        /// <summary>
        /// Posts an authorization event to the auditlog api.
        /// </summary>
        /// <param name="authEvent">The authorization event to be created</param>
        Task SaveAuthorizationEvent(AuthorizationEvent authorizationEvent);
    }
}
