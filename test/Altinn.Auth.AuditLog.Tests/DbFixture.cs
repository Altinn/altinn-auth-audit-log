using Altinn.Auth.AuditLog.Persistence.Configuration;
using Altinn.Authorization.ServiceDefaults.Npgsql.Yuniql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Altinn.Auth.AuditLog.Tests;

public class DbFixture 
    : IAsyncLifetime
{
    private const int MAX_CONCURRENCY = 20;

    Singleton.Ref<Inner>? _inner;

    public async Task InitializeAsync()
    {
        _inner = await Singleton.Get<Inner>();
    }

    public Task<OwnedDb> CreateDbAsync()
        => _inner!.Value.CreateDbAsync(this);

    private Task DropDbAsync(OwnedDb ownedDb)
        => _inner!.Value.DropDatabaseAsync(ownedDb);

    public async Task DisposeAsync()
    {
        if (_inner is { } inner)
        {
            await inner.DisposeAsync();
        }
    }

    private class Inner : IAsyncLifetime
    {
        private int _dbCounter = 0;
        private readonly AsyncLock _dbLock = new();
        private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder()
                    .WithImage("timescale/timescaledb:2.1.0-pg11")
                    .WithUsername("test-db-admin")
                    .WithPassword(Guid.NewGuid().ToString())
                    .WithDatabase("authauditlogdb")
                    .WithCleanUp(true)
                    .Build();

        private readonly AsyncConcurrencyLimiter _throtler = new(MAX_CONCURRENCY);

        string? _connectionString;
        NpgsqlDataSource? _db;

        public async Task InitializeAsync()
        {
            await _dbContainer.StartAsync();
            _connectionString = _dbContainer.GetConnectionString() + "; Include Error Detail=true; Pooling=false;";
            _db = NpgsqlDataSource.Create(_connectionString);
        }

        public async Task<OwnedDb> CreateDbAsync(DbFixture fixture)
        {
            var counter = Interlocked.Increment(ref _dbCounter);
            var dbName = $"test_{counter}";
            var appUserName = $"app_{counter}";
            var adminUserName = $"admin_{counter}";

            var appUserPassword = Guid.NewGuid().ToString();
            var adminUserPassword = Guid.NewGuid().ToString();

            var ticket = await _throtler.Acquire();

            try
            {
                // only create 1 db at once
                using var guard = await _dbLock.Acquire();

                await using var batch = _db!.CreateBatch();
                var cmd = batch.CreateBatchCommand();
                cmd.CommandText = /*strpsql*/$"""CREATE ROLE "{appUserName}" LOGIN PASSWORD '{appUserPassword}'""";
                batch.BatchCommands.Add(cmd);

                cmd = batch.CreateBatchCommand();
                cmd.CommandText = /*strpsql*/$"""CREATE ROLE "{adminUserName}" LOGIN PASSWORD '{adminUserPassword}'""";
                batch.BatchCommands.Add(cmd);

                cmd = batch.CreateBatchCommand();
                cmd.CommandText = /*strpsql*/$"""CREATE DATABASE "{dbName}" OWNER "{adminUserName}" """;
                batch.BatchCommands.Add(cmd);

                cmd = batch.CreateBatchCommand();
                cmd.CommandText = /*strpsql*/$"""GRANT CONNECT ON DATABASE "{dbName}" TO "{appUserName}" """;
                batch.BatchCommands.Add(cmd);

                await batch.ExecuteNonQueryAsync();

                var connectionStringBuilder = new NpgsqlConnectionStringBuilder(_connectionString) { Database = dbName, IncludeErrorDetail = true };

                connectionStringBuilder.Username = adminUserName;
                connectionStringBuilder.Password = adminUserPassword;
                var adminConnectionString = connectionStringBuilder.ConnectionString;

                connectionStringBuilder.Username = appUserName;
                connectionStringBuilder.Password = appUserPassword;
                var appConnectionString = connectionStringBuilder.ConnectionString;

                var ownedDb = new OwnedDb(adminConnectionString, appConnectionString, dbName, fixture, ticket);
                ticket = null;
                return ownedDb;
            }
            finally
            {
                ticket?.Dispose();
            }
        }

        public async Task DropDatabaseAsync(OwnedDb ownedDb)
        {
            await using var cmd = _db!.CreateCommand(/*strpsql*/$"DROP DATABASE IF EXISTS {ownedDb.DbName};");

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task DisposeAsync()
        {
            if (_db is { })
            {
                await _db.DisposeAsync();
            }

            await _dbContainer.DisposeAsync();
            _throtler.Dispose();
            _dbLock.Dispose();
        }
    }

    public sealed class OwnedDb : IAsyncDisposable
    {
        readonly string _adminConnectionString;
        readonly string _appConnectionString;
        readonly string _dbName;
        readonly DbFixture _db;
        readonly IDisposable _ticket;

        public OwnedDb(string adminConnectionString, string appConnectionString, string dbName, DbFixture db, IDisposable ticket)
        {
            _adminConnectionString = adminConnectionString;
            _appConnectionString = appConnectionString;
            _dbName = dbName;
            _db = db;
            _ticket = ticket;
        }

        public string AdminConnectionString => _adminConnectionString;

        public string AppConnectionString => _appConnectionString;

        internal string DbName => _dbName;

        public void ConfigureApplication(IHostApplicationBuilder builder)
        {
            var serviceDescriptor = builder.GetAltinnServiceDescriptor();
            ConfigureConfiguration(builder.Configuration, serviceDescriptor.Name);
            ConfigureServices(builder.Services, serviceDescriptor.Name);
        }

        public void ConfigureConfiguration(IConfigurationBuilder builder, string serviceName)
        {
            builder.AddInMemoryCollection([
                new($"Altinn:Npgsql:auditlog:ConnectionString", _appConnectionString),
                new($"Altinn:Npgsql:auditlog:Migrate:ConnectionString", _adminConnectionString),
            ]);
        }

        public void ConfigureServices(IServiceCollection services, string serviceName)
        {
            services.AddOptions<YuniqlDatabaseMigratorOptions>()
                .Configure(cfg =>
                {
                    cfg.Environment = "integrationtest";
                });
        }

        public async ValueTask DisposeAsync()
        {
            await _db.DropDbAsync(this);
            _ticket.Dispose();
        }
    }
}
