using Altinn.Auth.AuditLog;
using Microsoft.IdentityModel.Logging;
using System;

WebApplication app = AuditLogHost.Create(args);

app.AddDefaultAltinnMiddleware(errorHandlingPath: "/auditlog/api/v1/error");
Console.WriteLine($"The application environment{app.Environment}");

if (app.Environment.IsDevelopment())
{
    // Enable higher level of detail in exceptions related to JWT validation
    IdentityModelEventSource.ShowPII = true;

    // Enable Swagger
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();

app.MapDefaultAltinnEndpoints();
app.MapControllers();

app.Run();

/// <summary>
/// Startup class.
/// </summary>
public partial class Program
{
}
