using Altinn.Auth.AuditLog.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altinn.Auth.AuditLog.Core.Services.Interfaces
{
    /// <summary>
    /// service for authentication events
    /// </summary>
    public interface IAuthenticationEventService
    {
        /// <summary>
        /// Logs an authentication event
        /// </summary>
        /// <param name="authenticationEvent">the authentication event</param>
        /// <returns></returns>
        public Task CreateAuthenticationEvent(AuthenticationEvent authenticationEvent);
    }
}
