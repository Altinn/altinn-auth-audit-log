#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:9.0@sha256:1e12c265e1e1b3714c5805ab0cab63380eb687b0a04f3b3ef3392494a6122614 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:9.0@sha256:c5b188baf837b0180a14f988815b9cc7a55b836dd6cbe1b6e6523cf3098faaa8 AS build
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