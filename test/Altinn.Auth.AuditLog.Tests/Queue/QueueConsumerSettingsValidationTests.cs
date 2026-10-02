using Altinn.Auth.AuditLog.Configuration;
using Altinn.Auth.AuditLog.Queue;

namespace Altinn.Auth.AuditLog.Tests.Queue;

public class QueueConsumerSettingsValidationTests
{
    [Fact]
    public void Disabled_IsAlwaysValid()
    {
        var settings = new QueueConsumerSettings { Enabled = false, Authorization = { Concurrency = 0, BatchSize = 99 } };

        Assert.True(QueueConsumerDependencyInjectionExtensions.ValidateSettings(settings, out _));
    }

    [Fact]
    public void Defaults_WithConnectionString_AreValid()
    {
        var settings = Valid();

        Assert.True(QueueConsumerDependencyInjectionExtensions.ValidateSettings(settings, out var error), error);
    }

    [Fact]
    public void Enabled_WithoutConnection_IsInvalid()
    {
        var settings = Valid();
        settings.ConnectionString = null;
        settings.ServiceUri = null;

        Assert.False(QueueConsumerDependencyInjectionExtensions.ValidateSettings(settings, out var error));
        Assert.Contains("ConnectionString or ServiceUri", error);
    }

    [Fact]
    public void Enabled_WithRelativeServiceUri_IsInvalid()
    {
        var settings = Valid();
        settings.ConnectionString = null;
        settings.ServiceUri = "not a uri";

        Assert.False(QueueConsumerDependencyInjectionExtensions.ValidateSettings(settings, out var error));
        Assert.Contains("ServiceUri", error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65)]
    public void NestedQueue_ConcurrencyOutOfRange_IsInvalid(int concurrency)
    {
        var settings = Valid();
        settings.Authorization.Concurrency = concurrency;

        Assert.False(QueueConsumerDependencyInjectionExtensions.ValidateSettings(settings, out var error));
        Assert.Contains("Authorization", error);
        Assert.Contains("Concurrency", error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(33)]
    public void NestedQueue_BatchSizeOutOfRange_IsInvalid(int batchSize)
    {
        var settings = Valid();
        settings.Authentication.BatchSize = batchSize;

        Assert.False(QueueConsumerDependencyInjectionExtensions.ValidateSettings(settings, out var error));
        Assert.Contains("Authentication", error);
        Assert.Contains("BatchSize", error);
    }

    [Fact]
    public void NestedQueue_MissingQueueName_IsInvalid()
    {
        var settings = Valid();
        settings.Authorization.QueueName = " ";

        Assert.False(QueueConsumerDependencyInjectionExtensions.ValidateSettings(settings, out var error));
        Assert.Contains("QueueName", error);
    }

    [Fact]
    public void NestedQueue_NonPositiveVisibilityTimeout_IsInvalid()
    {
        var settings = Valid();
        settings.Authorization.VisibilityTimeout = TimeSpan.Zero;

        Assert.False(QueueConsumerDependencyInjectionExtensions.ValidateSettings(settings, out var error));
        Assert.Contains("VisibilityTimeout", error);
    }

    [Fact]
    public void NestedQueue_VisibilityTimeoutAboveAzureMax_IsInvalid()
    {
        var settings = Valid();
        settings.Authorization.VisibilityTimeout = TimeSpan.FromDays(8);

        Assert.False(QueueConsumerDependencyInjectionExtensions.ValidateSettings(settings, out var error));
        Assert.Contains("7 days", error);
    }

    [Fact]
    public void NestedQueue_Disabled_IsNotValidated()
    {
        var settings = Valid();
        settings.Authentication.Enabled = false;
        settings.Authentication.Concurrency = 0;
        settings.Authentication.QueueName = string.Empty;

        Assert.True(QueueConsumerDependencyInjectionExtensions.ValidateSettings(settings, out var error), error);
    }

    private static QueueConsumerSettings Valid()
        => new() { Enabled = true, ConnectionString = "UseDevelopmentStorage=true" };
}
