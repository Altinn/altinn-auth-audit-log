using Altinn.Auth.AuditLog.Functions.Models;
using Azure.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
