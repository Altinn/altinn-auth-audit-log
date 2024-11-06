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
        /// Configures the resource registry host.
        /// </summary>
        /// <param name="args">The command line arguments.</param>
        public static WebApplication Create(string[] args)
        {
            var builder = AltinnHost.CreateWebApplicationBuilder("auditlog", args);
            var services = builder.Services;
            var config = builder.Configuration;

            MapPostgreSqlConfiguration(builder);
            services.AddMemoryCache();

            services.Configure<KeyVaultSettings>(config.GetSection("kvSetting"));
            builder.AddAuditLogPersistence();
            services.AddHealthChecks().AddCheck<HealthCheck>("auditlog_ui_health_check");
            builder.Services.AddSingleton<PartitionCreationHostedService>();
            builder.Services.AddHostedService(sp => sp.GetRequiredService<PartitionCreationHostedService>());
            services.AddSingleton<IAuthenticationEventService, AuthenticationEventService>();
            services.AddSingleton<IAuthorizationEventService, AuthorizationEventService>();           
            services.Configure<PostgreSQLSettings>(config.GetSection("PostgreSQLSettings"));

            builder.Services.AddControllers(
                options => options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true);
            builder.Services.AddScoped<ValidationFilterAttribute>();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();
            return builder.Build();
        }

        // Note: eventually we can rename the configuration values and remove this mapping
        private static void MapPostgreSqlConfiguration(IHostApplicationBuilder builder)
        {
            var runMigrations = builder.Configuration.GetValue<bool>("PostgreSQLSettings:EnableDBConnection");
            var adminConnectionStringFmt = builder.Configuration.GetValue<string>("PostgreSQLSettings:AdminConnectionString");
            var adminConnectionStringPwd = builder.Configuration.GetValue<string>("PostgreSQLSettings:AuthAuditLogDbAdminPwd");
            var connectionStringFmt = builder.Configuration.GetValue<string>("PostgreSQLSettings:ConnectionString");
            var connectionStringPwd = builder.Configuration.GetValue<string>("PostgreSQLSettings:AuthAuditLogDbPwd");

            var adminConnectionString = string.Format(adminConnectionStringFmt, adminConnectionStringPwd);
            var connectionString = string.Format(connectionStringFmt, connectionStringPwd);

            var serviceDescriptor = builder.Services.GetAltinnServiceDescriptor();
            var existingConnString = builder.Configuration.GetValue<string>($"ConnectionStrings:{serviceDescriptor.Name}_db");
            var existingNpgsqlString = builder.Configuration.GetValue<string>($"Altinn:Npgsql:{serviceDescriptor.Name}:ConnectionString");

            if (!string.IsNullOrEmpty(existingConnString) || !string.IsNullOrEmpty(existingNpgsqlString))
            {
                return;
            }

            builder.Configuration.AddInMemoryCollection([
                new($"Altinn:Npgsql:{serviceDescriptor.Name}:ConnectionString", connectionString),
                new($"Altinn:Npgsql:{serviceDescriptor.Name}:Migrate:ConnectionString", adminConnectionString),
                new($"Altinn:Npgsql:{serviceDescriptor.Name}:Migrate:Enabled", runMigrations ? "true" : "false"),
            ]);
        }
    }
}
