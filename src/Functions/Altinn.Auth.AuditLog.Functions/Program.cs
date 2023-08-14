using Altinn.Auth.AuditLog.Functions.Clients;
using Altinn.Auth.AuditLog.Functions.Clients.Interfaces;
using Altinn.Auth.AuditLog.Functions.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(s=>
    {
        s.AddOptions<PlatformSettings>().Configure<IConfiguration>((settings, configuration) =>
        {
            configuration.GetSection("Platform").Bind(settings);
        });
        s.AddHttpClient<IAuditLogClient, AuditLogClient>();
    })
    .Build();

host.Run();
