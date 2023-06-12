using Altinn.Auth.AuditLog.Controllers;
using Altinn.Auth.AuditLog.Core.Repositories.Interfaces;
using Altinn.Auth.AuditLog.Core.Services;
using Altinn.Auth.AuditLog.Core.Services.Interfaces;
using Altinn.Auth.AuditLog.Persistence;
using Altinn.Auth.AuditLog.Tests.Mocks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altinn.Auth.AuditLog.Tests.Utils
{
    public static class SetupUtil
    {
        public static HttpClient GetTestClient(
            CustomWebApplicationFactory<AuthenticationEventController> customFactory)
        {
            WebApplicationFactory<AuthenticationEventController> factory = customFactory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton<IAuthenticationEventRepository, AuthenticationEventRepositoryMock>();
                    services.AddSingleton<IAuthenticationEventService, AuthenticationEventService>();

                });
            });
            factory.Server.AllowSynchronousIO = true;
            return factory.CreateClient();
        }

        public static HttpClient GetTestClient(
            CustomWebApplicationFactory<AuthorizationEventController> customFactory)
        {
            WebApplicationFactory<AuthorizationEventController> factory = customFactory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton<IAuthenticationEventRepository, AuthenticationEventRepositoryMock>();
                    services.AddSingleton<IAuthorizationEventService, AuthorizationEventService>();

                });
            });
            factory.Server.AllowSynchronousIO = true;
            return factory.CreateClient();
        }
    }
}
