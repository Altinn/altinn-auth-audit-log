#nullable enable
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;
using Npgsql;
using Xunit;

namespace Altinn.Auth.AuditLog.Tests;

public abstract class WebApplicationTests
    : IClassFixture<DbFixture>
    , IClassFixture<WebApplicationFixture>
    , IAsyncLifetime
{
    private readonly DbFixture _dbFixture;
    private readonly WebApplicationFixture _webApplicationFixture;

    public WebApplicationTests(DbFixture dbFixture, WebApplicationFixture webApplicationFixture)
    {
        _dbFixture = dbFixture;
        _webApplicationFixture = webApplicationFixture;
    }

    private WebApplicationFactory<Program>? _webApp;
    private IServiceProvider? _services;
    private AsyncServiceScope _scope;
    private DbFixture.OwnedDb? _db;

    protected IServiceProvider Services => _scope!.ServiceProvider;
    protected NpgsqlDataSource DataSource => Services.GetRequiredService<NpgsqlDataSource>();
    protected FakeTimeProvider TimeProvider => Services.GetRequiredService<FakeTimeProvider>();

    protected HttpClient CreateClient()
        => _webApp!.CreateClient();

    protected virtual ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    protected virtual void ConfigureTestConfiguration(IConfigurationBuilder builder)
    {
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
        await _scope.DisposeAsync();
        if (_webApp is { } webApp) await webApp.DisposeAsync();

        if (_db is { } db) await db.DisposeAsync();

    }

    async Task IAsyncLifetime.InitializeAsync()
    {
        _db = await _dbFixture.CreateDbAsync();
        _webApp = _webApplicationFixture.CreateServer(
            configureConfiguration: config =>
            {
                _db.ConfigureConfiguration(config, "auditlog");
                ConfigureTestConfiguration(config);
            },
            configureServices: services =>
            {
                _db.ConfigureServices(services, "auditlog");              
            });

        _services = _webApp.Services;
        _scope = _services.CreateAsyncScope();
    }
}
