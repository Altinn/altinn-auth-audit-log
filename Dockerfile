# Building the auditlog api
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
COPY src .
WORKDIR Altinn.Auth.AuditLog/
RUN dotnet build Altinn.Auth.AuditLog.csproj -c Release -o /app_output
RUN dotnet publish Altinn.Auth.AuditLog.csproj -c Release -o /app_output

# Building the final image
FROM mcr.microsoft.com/dotnet/aspnet:7.0-alpine AS final
EXPOSE 5166
WORKDIR /app
COPY --from=build /app_output .
COPY src/Altinn.Auth.AuditLog/Migration ./Migration

ENTRYPOINT ["dotnet", "Altinn.Auth.AuditLog.dll"]
