using Altinn.Auth.AuditLog.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altinn.Auth.AuditLog.Core.Repositories.Interfaces
{
    /// <summary>
    /// Interface for PostgresSQL operations on authorization event
    /// </summary>
    public interface IAuthorizationEventRepository
    {
        Task InsertAuthorizationEvent(AuthorizationEvent authorizationEvent);
    }
}
