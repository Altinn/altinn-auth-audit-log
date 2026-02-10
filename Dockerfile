# Building the auditlog api
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine@sha256:9b4b31da5246f575086b1901e9871b189ae2a80eb42fe9234e9d000b51febd4b AS build
ARG SOURCE_REVISION_ID=LOCALBUILD

COPY src .
WORKDIR Altinn.Auth.AuditLog/
RUN echo "Building Altinn.Auth.AuditLog with SourceRevisionId=${SOURCE_REVISION_ID}"
RUN dotnet build Altinn.Auth.AuditLog.csproj -c Release -o /app_output -p SourceRevisionId=${SOURCE_REVISION_ID}
RUN dotnet publish Altinn.Auth.AuditLog.csproj -c Release -o /app_output -p SourceRevisionId=${SOURCE_REVISION_ID}

# Building the final image
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine@sha256:049f2d7d7acfcbf09e1d15eb4faccec6453b0a98f0cb54d53bcbdc3ed91e96c8 AS final
EXPOSE 5166
WORKDIR /app
COPY --from=build /app_output .
RUN mkdir /tmp/logtelemetry
ENTRYPOINT ["dotnet", "Altinn.Auth.AuditLog.dll"]
