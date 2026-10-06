namespace Altinn.Auth.AuditLog.Configuration;

/// <summary>
/// Bridges the standard Azure variable <c>APPLICATIONINSIGHTS_CONNECTION_STRING</c> to the configuration keys
/// <c>Altinn.Authorization.ServiceDefaults</c> reads when deciding whether to export OpenTelemetry to Application
/// Insights (<c>ConnectionStrings:ApplicationInsights</c>, <c>ApplicationInsights:ConnectionString</c> or
/// <c>ApplicationInsights:InstrumentationKey</c>). Without this, a Container App that only sets the Azure
/// variable collects metrics in-process but exports nothing, and the only sign is the start-up log line
/// "No ApplicationInsights connection string - skipping Application Insights".
/// </summary>
internal static class ApplicationInsightsEnvironment
{
    /// <summary>
    /// The variable Azure (and the Azure Monitor SDKs) use by convention.
    /// </summary>
    public const string AzureVariable = "APPLICATIONINSIGHTS_CONNECTION_STRING";

    /// <summary>
    /// The environment variable form of <c>ConnectionStrings:ApplicationInsights</c>, which ServiceDefaults prefers.
    /// </summary>
    public const string TargetVariable = "ConnectionStrings__ApplicationInsights";

    private static readonly string[] _serviceDefaultsVariables =
    [
        TargetVariable,
        "ApplicationInsights__ConnectionString",
        "ApplicationInsights__InstrumentationKey",
    ];

    /// <summary>
    /// Maps <see cref="AzureVariable"/> to <see cref="TargetVariable"/> in the process environment, unless one of
    /// the ServiceDefaults variables is already set. Must run before the host builder reads configuration.
    /// </summary>
    /// <returns><see langword="true"/> if a mapping was made.</returns>
    public static bool MapAzureVariable()
        => MapAzureVariable(Environment.GetEnvironmentVariable, Environment.SetEnvironmentVariable);

    /// <summary>
    /// Testable core of <see cref="MapAzureVariable()"/>.
    /// </summary>
    internal static bool MapAzureVariable(Func<string, string?> getVariable, Action<string, string> setVariable)
    {
        var value = getVariable(AzureVariable);
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        foreach (var variable in _serviceDefaultsVariables)
        {
            if (!string.IsNullOrWhiteSpace(getVariable(variable)))
            {
                return false;
            }
        }

        setVariable(TargetVariable, value);
        return true;
    }
}
