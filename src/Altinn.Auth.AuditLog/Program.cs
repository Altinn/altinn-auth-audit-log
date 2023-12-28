using Altinn.Auth.AuditLog.Configuration;
using Altinn.Auth.AuditLog.Core.Models;
using Altinn.Auth.AuditLog.Core.Repositories.Interfaces;
using Altinn.Auth.AuditLog.Core.Services;
using Altinn.Auth.AuditLog.Core.Services.Interfaces;
using Altinn.Auth.AuditLog.Filters;
using Altinn.Auth.AuditLog.Health;
using Altinn.Auth.AuditLog.Persistence;
using Altinn.Auth.AuditLog.Persistence.Configuration;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.ApplicationInsights.AspNetCore.Extensions;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.WindowsServer.TelemetryChannel;
using Microsoft.Extensions.Logging.ApplicationInsights;
using Yuniql.AspNetCore;
using Yuniql.PostgreSql;
var builder = WebApplication.CreateBuilder(args);

ILogger logger;

string applicationInsightsKeySecretName = "ApplicationInsights--InstrumentationKey";
string applicationInsightsConnectionString = string.Empty;

ConfigureSetupLogging();

await SetConfigurationProviders(builder.Configuration);

ConfigureLogging(builder.Logging);


// Add services to the container.
ConfigureServices(builder.Services, builder.Configuration);

builder.Services.AddControllers(
    options => options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true);
builder.Services.AddScoped<ValidationFilterAttribute>(); 
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.MapHealthChecks("/health");

ConfigurePostgreSql();

app.Run();

void ConfigurePostgreSql()
{
    if (builder.Configuration.GetValue<bool>("PostgreSQLSettings:EnableDBConnection"))
    {
        ConsoleTraceService traceService = new ConsoleTraceService { IsDebugEnabled = true };

        string connectionString = string.Format(
            builder.Configuration.GetValue<string>("PostgreSQLSettings:AdminConnectionString"),
            builder.Configuration.GetValue<string>("PostgreSQLSettings:AuthAuditLogDbAdminPwd"));

        string workspacePath = Path.Combine(Environment.CurrentDirectory, builder.Configuration.GetValue<string>("PostgreSQLSettings:WorkspacePath"));
        if (builder.Environment.IsDevelopment())
        {
            workspacePath = Path.Combine(Directory.GetParent(Environment.CurrentDirectory).FullName, builder.Configuration.GetValue<string>("PostgreSQLSettings:WorkspacePath"));
        }

        app.UseYuniql(
            new PostgreSqlDataService(traceService),
            new PostgreSqlBulkImportService(traceService),
            traceService,
            new Yuniql.AspNetCore.Configuration
            {
                Workspace = workspacePath,
                ConnectionString = connectionString,
                IsAutoCreateDatabase = false,
                IsDebug = true,
            });
    }
}

void ConfigureServices(IServiceCollection services, IConfiguration config)
{
    string connectionString = string.Format(
    builder.Configuration.GetValue<string>("PostgreSQLSettings:AdminConnectionString"),
    builder.Configuration.GetValue<string>("PostgreSQLSettings:AuthAuditLogDbAdminPwd"));
    bool logParameters = builder.Configuration.GetValue<bool>("PostgreSQLSettings:LogParameters");
    services.AddHealthChecks().AddCheck<HealthCheck>("auditlog_ui_health_check");
    services.AddSingleton<IAuthenticationEventService, AuthenticationEventService>();
    services.AddSingleton<IAuthenticationEventRepository, AuthenticationEventRepository>();
    services.AddSingleton<IAuthorizationEventService, AuthorizationEventService>();
    services.AddSingleton<IAuthorizationEventRepository, AuthorizationEventRepository>();
    services.Configure<PostgreSQLSettings>(config.GetSection("PostgreSQLSettings"));
    services.Configure<KeyVaultSettings>(config.GetSection("KeyVaultSettings"));
    services.AddNpgsqlDataSource(connectionString, builder => builder.EnableParameterLogging(logParameters));

    if (!string.IsNullOrEmpty(applicationInsightsConnectionString))
    {
        services.AddSingleton(typeof(ITelemetryChannel), new ServerTelemetryChannel
        { StorageFolder = "/tmp/logtelemetry" });
        services.AddApplicationInsightsTelemetry(new ApplicationInsightsServiceOptions
        {
            ConnectionString = applicationInsightsConnectionString,
        });

        services.AddApplicationInsightsTelemetryProcessor<HealthTelemetryFilter>();
        services.AddSingleton<ITelemetryInitializer, CustomTelemetryInitializer>();

        logger.LogInformation("Startup // ApplicationInsightsConnectionString = {applicationInsightsConnectionString}", applicationInsightsConnectionString);
    }
}

void ConfigureSetupLogging()
{
    // Setup logging for the web host creation
    ILoggerFactory logFactory = LoggerFactory.Create(builder =>
    {
        builder
            .AddFilter("Microsoft", LogLevel.Warning)
            .AddFilter("System", LogLevel.Warning)
            .AddFilter("Altinn.Auth.AuditLog.Program", LogLevel.Debug)
        .AddConsole();
    });

    logger = logFactory.CreateLogger<Program>();
}

void ConfigureLogging(ILoggingBuilder logging)
{
    // Clear log providers
    logging.ClearProviders();

    // Setup up application insight if ApplicationInsightsConnectionString is available
    if (!string.IsNullOrEmpty(applicationInsightsConnectionString))
    {
        // Add application insights https://docs.microsoft.com/en-us/azure/azure-monitor/app/ilogger
        logging.AddApplicationInsights(
            configureTelemetryConfiguration: config => config.ConnectionString = applicationInsightsConnectionString,
            configureApplicationInsightsLoggerOptions: options => { });

        // Optional: Apply filters to control what logs are sent to Application Insights.
        // The following configures LogLevel Information or above to be sent to
        // Application Insights for all categories.
        logging.AddFilter<ApplicationInsightsLoggerProvider>(string.Empty, LogLevel.Warning);

        // Adding the filter below to ensure logs of all severity from Program.cs
        // is sent to ApplicationInsights.
        logging.AddFilter<ApplicationInsightsLoggerProvider>(typeof(Program).FullName, LogLevel.Trace);
    }
    else
    {
        // If not application insight is available log to console
        logging.AddFilter("Microsoft", LogLevel.Warning);
        logging.AddFilter("System", LogLevel.Warning);
        logging.AddConsole();
    }
}

async Task SetConfigurationProviders(ConfigurationManager config)
{
    config.AddEnvironmentVariables();

    config.AddCommandLine(args);

    if (!builder.Environment.IsDevelopment())
    {
        await ConnectToKeyVaultAndSetApplicationInsights(config);
    }
}

async Task ConnectToKeyVaultAndSetApplicationInsights(ConfigurationManager config)
{
    logger.LogInformation("Program // Connect to key vault and set up application insights");

    KeyVaultSettings keyVaultSettings = new KeyVaultSettings();

    config.GetSection("KeyVaultSettings").Bind(keyVaultSettings);
    try
    {
        SecretClient client = new SecretClient(new Uri(keyVaultSettings.SecretUri), new DefaultAzureCredential());
        KeyVaultSecret secret = await client.GetSecretAsync(applicationInsightsKeySecretName);
        applicationInsightsConnectionString = string.Format("InstrumentationKey={0}", secret.Value);
    }
    catch (Exception vaultException)
    {
        logger.LogError(vaultException, "Unable to read application insights key.");
    }

    try
    {
        config.AddAzureKeyVault(new Uri(keyVaultSettings.SecretUri), new DefaultAzureCredential());
    }
    catch (Exception vaultException)
    {
        logger.LogError(vaultException, "Unable to add key vault secrets to config.");
    }
}