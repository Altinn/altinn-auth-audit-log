# Building the auditlog api
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine@sha256:2303ad5956875eb82d3c6195e43f0e8e1378a6252869f2d4d200e067130ff5b5 AS build
ARG SOURCE_REVISION_ID=LOCALBUILD

COPY src .
WORKDIR Altinn.Auth.AuditLog/
RUN echo "Building Altinn.Auth.AuditLog with SourceRevisionId=${SOURCE_REVISION_ID}"
RUN dotnet build Altinn.Auth.AuditLog.csproj -c Release -o /app_output -p SourceRevisionId=${SOURCE_REVISION_ID}
RUN dotnet publish Altinn.Auth.AuditLog.csproj -c Release -o /app_output -p SourceRevisionId=${SOURCE_REVISION_ID}

# Building the final image
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine@sha256:374a0ebc32ae59692470070a8bbcdef1186250d446836bf6ec8ac08a5c623667 AS final
EXPOSE 5166
WORKDIR /app
COPY --from=build /app_output .
RUN mkdir /tmp/logtelemetry
ENTRYPOINT ["dotnet", "Altinn.Auth.AuditLog.dll"]
