using Altinn.Auth.AuditLog.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altinn.Auth.AuditLog.Core.Repositories.Interfaces
{
    /// <summary>
    /// Interface for PostgresSQL operations on authentication event
    /// </summary>
    public interface IAuthenticationEventRepository
    {
        /// <summary>
        /// inserts an authentication event to the database
        /// </summary>
        /// <param name="authenticationEvent"></param>
        /// <returns></returns>
        Task InsertAuthenticationEvent(AuthenticationEvent authenticationEvent);

        /// <summary>
        /// Inserts a batch of authentication events in a single statement (and thus a single transaction).
        /// Either all events are inserted, or none are.
        /// </summary>
        /// <param name="authenticationEvents">The events to insert.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
        Task InsertAuthenticationEvents(IReadOnlyList<AuthenticationEvent> authenticationEvents, CancellationToken cancellationToken = default);
    }
}
