#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:9.0@sha256:bf48e8b328707fae0e63a1b7d764d770221def59b97468c8cdee68f4e38ddfb9 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:9.0@sha256:0d84f05256dec37a5d1739158fd5ea197b8ad3b4e8d0e32be47b754db5963a9e AS build
WORKDIR /src
COPY ["Altinn.Auth.AuditLog/Altinn.Auth.AuditLog.csproj", "Altinn.Auth.AuditLog/"]
RUN dotnet restore "Altinn.Auth.AuditLog/Altinn.Auth.AuditLog.csproj"
COPY . .
WORKDIR "/src/Altinn.Auth.AuditLog"
RUN dotnet build "Altinn.Auth.AuditLog.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Altinn.Auth.AuditLog.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Altinn.Auth.AuditLog.dll"]
