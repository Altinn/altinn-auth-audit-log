# Building the auditlog api
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
ARG SOURCE_REVISION_ID=LOCALBUILD

COPY src .
WORKDIR Altinn.Auth.AuditLog/
RUN echo "Building Altinn.Auth.AuditLog with SourceRevisionId=${SOURCE_REVISION_ID}"
RUN dotnet build Altinn.Auth.AuditLog.csproj -c Release -o /app_output -p SourceRevisionId=${SOURCE_REVISION_ID}
RUN dotnet publish Altinn.Auth.AuditLog.csproj -c Release -o /app_output -p SourceRevisionId=${SOURCE_REVISION_ID}

# Building the final image
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS final
EXPOSE 5166
WORKDIR /app
COPY --from=build /app_output .
COPY src/Altinn.Auth.AuditLog/Migration ./Migration
RUN mkdir /tmp/logtelemetry
ENTRYPOINT ["dotnet", "Altinn.Auth.AuditLog.dll"]
