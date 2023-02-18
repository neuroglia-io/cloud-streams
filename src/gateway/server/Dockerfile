#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["src/gateway/server/CloudStreams.Gateway.Api.Server.csproj", "src/gateway/server/"]
COPY ["src/core/infrastructure/event-store/event-store/CloudStreams.Core.Infrastructure.EventSourcing.EventStore.csproj", "src/core/infrastructure/event-store/event-store/"]
COPY ["src/core/infrastructure/abstractions/CloudStreams.Core.Infrastructure.Abstractions.csproj", "src/core/infrastructure/abstractions/"]
COPY ["src/core/data/CloudStreams.Core.Data.csproj", "src/core/data/"]
COPY ["src/core/core/CloudStreams.Core.csproj", "src/core/core/"]
COPY ["src/core/infrastructure/resource-repository/kubernetes/CloudStreams.Core.Infrastructure.ResourceRepository.Kubernetes.csproj", "src/core/infrastructure/resource-repository/kubernetes/"]
COPY ["src/core/infrastructure/schema-registry/apicurio/CloudStreams.Core.Infrastructure.SchemaRegistry.Apicurio.csproj", "src/core/infrastructure/schema-registry/apicurio/"]
COPY ["src/dashboard/ui/CloudStreams.Dashboard.csproj", "src/dashboard/ui/"]
COPY ["src/gateway/client/CloudStreams.Gateway.Api.Client.csproj", "src/gateway/client/"]
COPY ["src/dashboard/state-management/CloudStreams.Dashboard.StateManagement.csproj", "src/dashboard/state-management/"]
COPY ["src/gateway/api/CloudStreams.Gateway.Api.csproj", "src/gateway/api/"]
COPY ["src/gateway/application/CloudStreams.Gateway.Application.csproj", "src/gateway/application/"]
COPY ["src/core/application/CloudStreams.Core.Application.csproj", "src/core/application/"]
RUN dotnet restore "src/gateway/server/CloudStreams.Gateway.Api.Server.csproj"
COPY . .
WORKDIR "/src/src/gateway/server"
RUN dotnet build "CloudStreams.Gateway.Api.Server.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "CloudStreams.Gateway.Api.Server.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "CloudStreams.Gateway.Api.Server.dll"]