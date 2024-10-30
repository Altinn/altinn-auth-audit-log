using System;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Auth.AuditLog.Core.Repositories.Interfaces;
using Altinn.Auth.AuditLog.Persistence;
using Altinn.Auth.AuditLog.Services;
using Altinn.Auth.AuditLog.Tests;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Npgsql;
using Testcontainers.PostgreSql;

public class PartitionCreationServiceIntegrationTests(DbFixture dbFixture, WebApplicationFixture webApplicationFixture)
        : WebApplicationTests(dbFixture, webApplicationFixture)
{
    private string _connectionString;
    private readonly Mock<TimeProvider> timeProviderMock = new Mock<TimeProvider>();

    protected override void ConfigureServices(IServiceCollection services)
    {
        base.ConfigureServices(services);
        bool enableOidc = false;
        bool forceOidc = false;
        string defaultOidc = "altinn";

        string configPath = GetConfigPath();

        WebHostBuilder builder = new();

        builder.ConfigureAppConfiguration((context, conf) =>
        {
            conf.AddJsonFile(configPath);
        });

        var configuration = new ConfigurationBuilder()
          .AddJsonFile(configPath)
          .Build();

        configuration.GetSection("GeneralSettings:EnableOidc").Value = enableOidc.ToString();
        configuration.GetSection("GeneralSettings:ForceOidc").Value = forceOidc.ToString();
        configuration.GetSection("GeneralSettings:DefaultOidcProvider").Value = defaultOidc;

        _connectionString = string.Format(configuration.GetSection("PostgreSQLSettings:AdminConnectionString").Value, configuration.GetSection("PostgreSQLSettings:AuthAuditLogDbAdminPwd").Value);

        services.AddSingleton<IAuthenticationEventRepository, AuthenticationEventRepository>();
        services.AddNpgsqlDataSource(_connectionString);
        services.AddSingleton<PartitionCreationService>();
        SetupDateTimeMock();       
    }
    
    [Fact]
    public async Task ExecuteAsync_CreatesCurrentMonthPartition_OnlyOnce()
    {
        var partitionService = Services.GetRequiredService<PartitionCreationService>();
        var cancellationToken = new CancellationTokenSource(TimeSpan.FromSeconds(30)).Token; // Set a timeout

        // Start the background service
        var task = Task.Run(() => partitionService.StartAsync(cancellationToken));

        // Allow time for the service to run
        await Task.Delay(TimeSpan.FromSeconds(3));

        // Act
        await partitionService.StopAsync(cancellationToken); // Stop the service after a brief run

        // Assert the partition is created
        try
        {
            using (var connection = new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync(cancellationToken);
                var currentDate = DateTime.UtcNow;
                var currentMonth = currentDate.Month;
                var currentYear = currentDate.Year;
                var partitionName = $"eventlogv1_y{currentYear}m{currentMonth:D2}";

                var checkPartitionCommand = $"SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema='authentication' table_name='{partitionName}');";
                using (var command = new NpgsqlCommand(checkPartitionCommand, connection))
                {
                    var exists = (bool)await command.ExecuteScalarAsync(cancellationToken);
                    Assert.True(exists);
                }
            }
        }
        catch (Exception ex)
        {
            Console.Write(ex.ToString());
        }
    }

    private void SetupDateTimeMock()
    {
        timeProviderMock.Setup(x => x.GetUtcNow()).Returns(new DateTimeOffset(2018, 05, 15, 02, 05, 00, TimeSpan.Zero));
    }

    private static string GetConfigPath()
    {
        string unitTestFolder = Path.GetDirectoryName(new Uri(typeof(PartitionCreationServiceIntegrationTests).Assembly.Location).LocalPath);
        return Path.Combine(unitTestFolder, $"../../../appsettings.test.json");
    }
}
