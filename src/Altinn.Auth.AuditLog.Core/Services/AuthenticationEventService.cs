using Altinn.Auth.AuditLog.Core.Models;
using Altinn.Auth.AuditLog.Core.Repositories.Interfaces;
using Altinn.Auth.AuditLog.Core.Services.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altinn.Auth.AuditLog.Core.Services
{
    ///<inheritdoc/>
    public class AuthenticationEventService : IAuthenticationEventService
    {
        private readonly ILogger<IAuthenticationEventService> _logger;
        private readonly IAuthenticationEventRepository _authenticationEventRepository;

        public AuthenticationEventService(
            ILogger<AuthenticationEventService> logger,
            IAuthenticationEventRepository authenticationLogRepository) 
        { 
            _logger = logger;
            _authenticationEventRepository = authenticationLogRepository;
        }

        /// <inheritdoc/>
        public async Task CreateAuthenticationEvent(AuthenticationEvent authenticationEvent)
        {
            await _authenticationEventRepository.InsertAuthenticationEvent(authenticationEvent);
        }

        /// <inheritdoc/>
        public async Task CreateAuthenticationEvents(IReadOnlyList<AuthenticationEvent> authenticationEvents, CancellationToken cancellationToken = default)
        {
            await _authenticationEventRepository.InsertAuthenticationEvents(authenticationEvents, cancellationToken);
        }
    }
}
