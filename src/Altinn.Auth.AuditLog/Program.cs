using Altinn.Auth.AuditLog.Configuration;
using Altinn.Auth.AuditLog.Core.Repositories.Interfaces;
using Altinn.Auth.AuditLog.Core.Services;
using Altinn.Auth.AuditLog.Core.Services.Interfaces;
using Altinn.Auth.AuditLog.Filters;
using Altinn.Auth.AuditLog.Persistence;
using Altinn.Auth.AuditLog.Persistence.Configuration;
using Yuniql.AspNetCore;
using Yuniql.PostgreSql;
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
ConfigureServices(builder.Services, builder.Configuration);

builder.Services.AddControllers(
    options => options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true);
builder.Services.AddScoped<ValidationFilterAttribute>(); 
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

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
    services.AddSingleton<IAuthenticationEventService, AuthenticationEventService>();
    services.AddSingleton<IAuthenticationEventRepository, AuthenticationEventRepository>();
    services.AddSingleton<IAuthorizationEventService, AuthorizationEventService>();
    services.AddSingleton<IAuthorizationEventRepository, AuthorizationEventRepository>();
    services.Configure<PostgreSQLSettings>(config.GetSection("PostgreSQLSettings"));
}