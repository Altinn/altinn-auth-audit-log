# Building the auditlog api
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine@sha256:2244f80ac7179b0feaf83ffca8fe82d31fbced5b7e353755bf9515a420eba711 AS build
ARG SOURCE_REVISION_ID=LOCALBUILD

COPY src .
WORKDIR Altinn.Auth.AuditLog/
RUN echo "Building Altinn.Auth.AuditLog with SourceRevisionId=${SOURCE_REVISION_ID}"
RUN dotnet build Altinn.Auth.AuditLog.csproj -c Release -o /app_output -p SourceRevisionId=${SOURCE_REVISION_ID}
RUN dotnet publish Altinn.Auth.AuditLog.csproj -c Release -o /app_output -p SourceRevisionId=${SOURCE_REVISION_ID}

# Building the final image
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine@sha256:30fdbd1b5963bba6ed66190d72d877b750d4203a671c9b54592f4551b8c5a087 AS final
EXPOSE 5166
WORKDIR /app
COPY --from=build /app_output .
RUN mkdir /tmp/logtelemetry
ENTRYPOINT ["dotnet", "Altinn.Auth.AuditLog.dll"]
