# Building the auditlog api
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine@sha256:7d98d5883675c6bca25b1db91f393b24b85125b5b00b405e55404fd6b8d2aead AS build
ARG SOURCE_REVISION_ID=LOCALBUILD

COPY src .
WORKDIR Altinn.Auth.AuditLog/
RUN echo "Building Altinn.Auth.AuditLog with SourceRevisionId=${SOURCE_REVISION_ID}"
RUN dotnet build Altinn.Auth.AuditLog.csproj -c Release -o /app_output -p SourceRevisionId=${SOURCE_REVISION_ID}
RUN dotnet publish Altinn.Auth.AuditLog.csproj -c Release -o /app_output -p SourceRevisionId=${SOURCE_REVISION_ID}

# Building the final image
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine@sha256:8e21337e482e353c958681789872b3451e966e07c259b9a6f9a8b7902749a785 AS final
EXPOSE 5166
WORKDIR /app
COPY --from=build /app_output .
RUN mkdir /tmp/logtelemetry
ENTRYPOINT ["dotnet", "Altinn.Auth.AuditLog.dll"]
