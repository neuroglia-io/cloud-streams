#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["src/apps/api-server/CloudStreams.Api.Server.csproj", "src/apps/api-server/"]
COPY ["src/api/http/CloudStreams.Api.Http.csproj", "src/api/http/"]
COPY ["src/api/core/CloudStreams.Api.Core.csproj", "src/api/core/"]
COPY ["src/core/infrastructure/abstractions/CloudStreams.Infrastructure.Abstractions.csproj", "src/core/infrastructure/abstractions/"]
COPY ["src/core/data/CloudStreams.Data.csproj", "src/core/data/"]
COPY ["src/core/core/CloudStreams.Core.csproj", "src/core/core/"]
COPY ["src/core/infrastructure/event-store/event-store/CloudStreams.Infrastructure.EventSourcing.EventStore.csproj", "src/core/infrastructure/event-store/event-store/"]
COPY ["src/core/infrastructure/resource-repository/kubernetes/CloudStreams.Infrastructure.ResourceRepository.Kubernetes.csproj", "src/core/infrastructure/resource-repository/kubernetes/"]
COPY ["src/core/infrastructure/schema-registry/apicurio/CloudStreams.Infrastructure.SchemaRegistry.Apicurio.csproj", "src/core/infrastructure/schema-registry/apicurio/"]
COPY ["src/apps/dashboard/CloudStreams.Dashboard.csproj", "src/apps/dashboard/"]
RUN dotnet restore "src/apps/api-server/CloudStreams.Api.Server.csproj"
COPY . .
WORKDIR "/src/src/apps/api-server"
RUN dotnet build "CloudStreams.Api.Server.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "CloudStreams.Api.Server.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "CloudStreams.Api.Server.dll"]