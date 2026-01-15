using Altinn.Auth.AuditLog.Core.Models;
using Altinn.Auth.AuditLog.Core.Services;
using Altinn.Auth.AuditLog.Core.Services.Interfaces;
using Altinn.Auth.AuditLog.Filters;
using Altinn.Auth.AuditLog.Health;
using Altinn.Auth.AuditLog.Persistence.Configuration;
using Altinn.Auth.AuditLog.Services;
using Altinn.Authorization.ServiceDefaults;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Altinn.Auth.AuditLog
{
    internal static class AuditLogHost
    {
        /// <summary>
        /// Configures the auditlog host.
        /// </summary>
        /// <param name="args">The command line arguments.</param>
        public static WebApplication Create(string[] args)
        {
            var builder = AltinnHost.CreateWebApplicationBuilder("auditlog", args);
            var services = builder.Services;
            var config = builder.Configuration;

            services.AddMemoryCache();

            services.Configure<KeyVaultSettings>(config.GetSection("kvSetting"));
            builder.AddAuditLogPersistence();
            builder.Services.AddSingleton<PartitionCreationHostedService>();
            builder.Services.AddHostedService(sp => sp.GetRequiredService<PartitionCreationHostedService>());
            services.AddSingleton<IAuthenticationEventService, AuthenticationEventService>();
            services.AddSingleton<IAuthorizationEventService, AuthorizationEventService>();           
            services.Configure<PostgreSQLSettings>(config.GetSection("PostgreSQLSettings"));

            builder.Services
                .AddControllers(options => options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true)
                .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
            builder.Services.AddScoped<ValidationFilterAttribute>();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddOpenApi();
            return builder.Build();
        }
    }
}
