using Altinn.Auth.AuditLog.Core.Models;
using Altinn.Auth.AuditLog.Core.Repositories.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altinn.Auth.AuditLog.Tests.Mocks
{
    public class AuthorizationEventRepositoryMock : IAuthorizationEventRepository
    {
        public Task InsertAuthorizationEvent(AuthorizationEvent authorizationEvent)
        {
            return Task.FromResult(authorizationEvent);
        }
    }
}
