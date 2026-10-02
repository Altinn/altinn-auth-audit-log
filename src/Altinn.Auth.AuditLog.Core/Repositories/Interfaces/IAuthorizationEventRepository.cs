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

        /// <summary>
        /// Inserts a batch of authorization events in a single statement (and thus a single transaction).
        /// Either all events are inserted, or none are.
        /// </summary>
        /// <param name="authorizationEvents">The events to insert.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
        Task InsertAuthorizationEvents(IReadOnlyList<AuthorizationEvent> authorizationEvents, CancellationToken cancellationToken = default);
    }
}
