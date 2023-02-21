#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["src/broker/server/CloudStreams.Broker.Api.Server.csproj", "src/broker/server/"]
COPY ["src/core/infrastructure/event-store/event-store/CloudStreams.Core.Infrastructure.EventSourcing.EventStore.csproj", "src/core/infrastructure/event-store/event-store/"]
COPY ["src/core/infrastructure/abstractions/CloudStreams.Core.Infrastructure.Abstractions.csproj", "src/core/infrastructure/abstractions/"]
COPY ["src/core/data/CloudStreams.Core.Data.csproj", "src/core/data/"]
COPY ["src/core/core/CloudStreams.Core.csproj", "src/core/core/"]
COPY ["src/core/infrastructure/resource-repository/kubernetes/CloudStreams.Core.Infrastructure.ResourceRepository.Kubernetes.csproj", "src/core/infrastructure/resource-repository/kubernetes/"]
COPY ["src/core/infrastructure/schema-registry/apicurio/CloudStreams.Core.Infrastructure.SchemaRegistry.Apicurio.csproj", "src/core/infrastructure/schema-registry/apicurio/"]
COPY ["src/broker/api/CloudStreams.Broker.Api.csproj", "src/broker/api/"]
COPY ["src/broker/application/CloudStreams.Broker.Application.csproj", "src/broker/application/"]
COPY ["src/core/application/CloudStreams.Core.Application.csproj", "src/core/application/"]
RUN dotnet restore "src/broker/server/CloudStreams.Broker.Api.Server.csproj"
COPY . .
WORKDIR "/src/src/broker/server"
RUN dotnet build "CloudStreams.Broker.Api.Server.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "CloudStreams.Broker.Api.Server.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "CloudStreams.Broker.Api.Server.dll"]