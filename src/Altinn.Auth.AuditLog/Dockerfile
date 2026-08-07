#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:9.0@sha256:7799364313c08f464193c93a9db935d86697b2b25c96c89fb2b629bbf9877016 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:9.0@sha256:cb9d975bf57fd1b0915858d1db1184bea20f7f746f0536323fcab49673144e8c AS build
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
