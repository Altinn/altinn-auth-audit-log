using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Auth.AuditLog.Core.Repositories.Interfaces;
using Altinn.Auth.AuditLog.Persistence;
using Altinn.Auth.AuditLog.Services;
using Altinn.Auth.AuditLog.Tests;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Npgsql;
using Testcontainers.PostgreSql;
using static System.Net.Mime.MediaTypeNames;

namespace Altinn.Auth.AuditLog.Tests.Integration;

public class PartitionCreationHostedServiceIntegrationTests(DbFixture dbFixture, WebApplicationFixture webApplicationFixture)
        : WebApplicationTests(dbFixture, webApplicationFixture)
{   
    protected IPartitionManagerRepository Repository => Services.GetRequiredService<PartitionManagerRepository>();
    protected PartitionCreationHostedService HostedService => Services.GetRequiredService<PartitionCreationHostedService>();

    protected Task WaitForPartitionJob()
    {
        return HostedService.RunningJob;
    }

    [Fact]
    public async Task ExecuteAsync_CreatesCurrentMonthPartition_OnlyOnce()
    {
        TimeProvider.Advance(TimeSpan.FromDays(1) + TimeSpan.FromHours(1));
        await WaitForPartitionJob();

        // Assert the partition is created
        var currentDate = DateOnly.FromDateTime(TimeProvider.GetUtcNow().UtcDateTime);
        var currentMonth = currentDate.Month;
        var currentYear = currentDate.Year;
        var partitionName = $"eventlogv2_y{currentYear}m{currentMonth:D2}";

        var checkAuthenticationPartitionCommand = $"SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema='authentication' and table_name='{partitionName}');";
        await using NpgsqlCommand pgcom = DataSource.CreateCommand(checkAuthenticationPartitionCommand);
        var exists = (bool)await pgcom.ExecuteScalarAsync();
        Assert.True(exists);
        var checkAuthorizationPartitionCommand = $"SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema='authz' and table_name='{partitionName}');";
        await using NpgsqlCommand pgcomAuthz = DataSource.CreateCommand(checkAuthorizationPartitionCommand);
        exists = (bool)await pgcomAuthz.ExecuteScalarAsync();
        Assert.True(exists);
    }
}
