using Altinn.Auth.AuditLog.Core.Models;
using Altinn.Auth.AuditLog.Core.Repositories.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altinn.Auth.AuditLog.Tests.Mocks
{
    public class AuthenticationEventRepositoryMock : IAuthenticationEventRepository
    {
        public Task<AuthenticationEvent> InsertAuthenticationEvent(AuthenticationEvent authenticationEvent)
        {
            return Task.FromResult(authenticationEvent);
        }
    }
}
