using Altinn.Auth.AuditLog.Core.Repositories.Interfaces;
using Altinn.Auth.AuditLog.Persistence;
using Altinn.Auth.AuditLog.Persistence.Configuration;
using Altinn.Authorization.ServiceDefaults.Npgsql.Yuniql;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for adding resource registry services to the dependency injection container.
/// </summary>
public static class AuditLogDependencyInjectionExtensions 
{
    /// <summary>
    /// Registers auditlog persistence services with the dependency injection container of a host application.
    /// </summary>
    /// <param name="builder">The <see cref="IHostApplicationBuilder"/>.</param>
    /// <returns><paramref name="builder"/> for further chaining.</returns>
    public static IHostApplicationBuilder AddAuditLogPersistence(
        this IHostApplicationBuilder builder)
    {
        builder.AddAuthenticationEventRepository();
        builder.AddauthorizationEventRepository();
        builder.AddPartitionManagementRepository();

        return builder;
    }

    /// <summary>
    /// Registers a <see cref="IAuthenticationEventRepository"/> with the dependency injection container of a host application.
    /// </summary>
    /// <param name="builder">The <see cref="IHostApplicationBuilder"/>.</param>
    /// <returns><paramref name="builder"/> for further chaining.</returns>
    public static IHostApplicationBuilder AddAuthenticationEventRepository(
        this IHostApplicationBuilder builder)
    {
        builder.AddDatabase();

        builder.Services.TryAddTransient<IAuthenticationEventRepository, AuthenticationEventRepository>();

        return builder;
    }

    /// <summary>
    /// Registers a <see cref="IAuthorizationEventRepository"/> with the dependency injection container of a host application.
    /// </summary>
    /// <param name="builder">The <see cref="IHostApplicationBuilder"/>.</param>
    /// <returns><paramref name="builder"/> for further chaining.</returns>
    public static IHostApplicationBuilder AddauthorizationEventRepository(
        this IHostApplicationBuilder builder)
    {
        builder.AddDatabase();
        builder.Services.TryAddSingleton(TimeProvider.System);

        builder.Services.TryAddTransient<IAuthorizationEventRepository, AuthorizationEventRepository>();

        return builder;
    }

    /// <summary>
    /// Registers a <see cref="IPartitionManagerRepository"/> with the dependency injection container of a host application.
    /// </summary>
    /// <param name="builder">The <see cref="IHostApplicationBuilder"/>.</param>
    /// <returns><paramref name="builder"/> for further chaining.</returns>
    public static IHostApplicationBuilder AddPartitionManagementRepository(
        this IHostApplicationBuilder builder)
    {
        builder.AddAdminDatabase();
        builder.Services.TryAddSingleton<IPartitionManagerRepository, PartitionManagerRepository>();

        return builder;
    }

    private static IHostApplicationBuilder AddDatabase(this IHostApplicationBuilder builder)
    {
        if (builder.Services.Contains(Marker.ServiceDescriptor))
        {
            // already added
            return builder;
        }

        builder.Services.Add(Marker.ServiceDescriptor);

        var fs = new ManifestEmbeddedFileProvider(typeof(AuditLogDependencyInjectionExtensions).Assembly, "Migration");
        string? migrationConnectionString = null;

        builder.AddAltinnPostgresDataSource(cfg => migrationConnectionString = cfg.Migrate.ConnectionString)
            .AddYuniqlMigrations(typeof(Marker), cfg =>
            {
                cfg.WorkspaceFileProvider = fs;
                cfg.Workspace = "/";
            });

        builder.Services.AddSingleton(new AdminDbSettings
        {
            ConnectionString = migrationConnectionString!,
        });

        return builder;
    }

    private static IHostApplicationBuilder AddAdminDatabase(this IHostApplicationBuilder builder)
    {
        builder.AddDatabase();

        builder.Services.AddKeyedSingleton<NpgsqlDataSource>(
            serviceKey: typeof(IPartitionManagerRepository),
            (sp, _) =>
            {
                var options = sp.GetRequiredService<AdminDbSettings>();
                var connectionStringBuilder = new NpgsqlConnectionStringBuilder(options.ConnectionString);
                connectionStringBuilder.Pooling = false;

                var builder = new NpgsqlDataSourceBuilder(connectionStringBuilder.ConnectionString);
                builder.UseLoggerFactory(sp.GetRequiredService<ILoggerFactory>());
                return builder.BuildMultiHost();
            });

        return builder;
    }

    private sealed class Marker
    {
        public static readonly ServiceDescriptor ServiceDescriptor = ServiceDescriptor.Singleton<Marker, Marker>();
    }

    private sealed class AdminDbSettings
    {
        public required string ConnectionString { get; init; }
    }
}
