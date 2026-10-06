using Altinn.Auth.AuditLog.Configuration;

namespace Altinn.Auth.AuditLog.Tests.Configuration;

public class ApplicationInsightsEnvironmentTests
{
    [Fact]
    public void MapsAzureVariable_WhenNoServiceDefaultsKeyIsSet()
    {
        var env = new Dictionary<string, string?> { [ApplicationInsightsEnvironment.AzureVariable] = "InstrumentationKey=abc;IngestionEndpoint=https://x" };

        var mapped = ApplicationInsightsEnvironment.MapAzureVariable(k => env.GetValueOrDefault(k), (k, v) => env[k] = v);

        Assert.True(mapped);
        Assert.Equal("InstrumentationKey=abc;IngestionEndpoint=https://x", env[ApplicationInsightsEnvironment.TargetVariable]);
    }

    [Fact]
    public void DoesNothing_WhenAzureVariableIsMissing()
    {
        var env = new Dictionary<string, string?>();

        var mapped = ApplicationInsightsEnvironment.MapAzureVariable(k => env.GetValueOrDefault(k), (k, v) => env[k] = v);

        Assert.False(mapped);
        Assert.Empty(env);
    }

    [Theory]
    [InlineData("ConnectionStrings__ApplicationInsights")]
    [InlineData("ApplicationInsights__ConnectionString")]
    [InlineData("ApplicationInsights__InstrumentationKey")]
    public void DoesNotOverride_WhenAServiceDefaultsKeyIsAlreadySet(string existing)
    {
        var env = new Dictionary<string, string?>
        {
            [ApplicationInsightsEnvironment.AzureVariable] = "InstrumentationKey=azure",
            [existing] = "already-configured",
        };

        var mapped = ApplicationInsightsEnvironment.MapAzureVariable(k => env.GetValueOrDefault(k), (k, v) => env[k] = v);

        Assert.False(mapped);
        Assert.Equal("already-configured", env[existing]);
    }
}
