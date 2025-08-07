#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:9.0@sha256:2b70ef1451cb01c2118969ad15fd0ae40fdbac158f09699fefae0dc32c4ca352 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:9.0@sha256:840f3b62b9742dde4461a3c31e38ffd34d41d7d33afd39c378cfcfd5dcb82bd5 AS build
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