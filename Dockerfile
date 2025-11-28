# Building the auditlog api
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine@sha256:7d98d5883675c6bca25b1db91f393b24b85125b5b00b405e55404fd6b8d2aead AS build
ARG SOURCE_REVISION_ID=LOCALBUILD

COPY src .
WORKDIR Altinn.Auth.AuditLog/
RUN echo "Building Altinn.Auth.AuditLog with SourceRevisionId=${SOURCE_REVISION_ID}"
RUN dotnet build Altinn.Auth.AuditLog.csproj -c Release -o /app_output -p SourceRevisionId=${SOURCE_REVISION_ID}
RUN dotnet publish Altinn.Auth.AuditLog.csproj -c Release -o /app_output -p SourceRevisionId=${SOURCE_REVISION_ID}

# Building the final image
FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:7c4246c1c384319346d45b3e24a10a21d5b6fc9b36a04790e1588148ff8055b0 AS final
EXPOSE 5166
WORKDIR /app
COPY --from=build /app_output .
RUN mkdir /tmp/logtelemetry
ENTRYPOINT ["dotnet", "Altinn.Auth.AuditLog.dll"]
