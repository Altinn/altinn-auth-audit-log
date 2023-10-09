using Altinn.Auth.AuditLog.Core.Models;
using Altinn.Auth.AuditLog.Core.Repositories;
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
    public class AuthorizationEventService : IAuthorizationEventService
    {
        private readonly ILogger<AuthorizationEventService> _logger;
        private readonly IAuthorizationEventRepository _authorizationEventRepository;

        public AuthorizationEventService(
            ILogger<AuthorizationEventService> logger,
            IAuthorizationEventRepository authorizationLogRepository) 
        { 
            _logger = logger;
            _authorizationEventRepository = authorizationLogRepository;
        }

        /// <inheritdoc/>
        public async Task CreateAuthorizationEvent(AuthorizationEvent authorizationEvent)
        {
            await _authorizationEventRepository.InsertAuthorizationEvent(authorizationEvent);
        }
    }
}
