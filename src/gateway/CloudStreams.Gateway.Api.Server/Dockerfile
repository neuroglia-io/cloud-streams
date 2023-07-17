#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 80
RUN apt-get update
RUN apt-get install -y jq

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["src/gateway/CloudStreams.Gateway.Api.Server/CloudStreams.Gateway.Api.Server.csproj", "src/gateway/CloudStreams.Gateway.Api.Server/"]
COPY ["src/gateway/CloudStreams.Gateway.Api/CloudStreams.Gateway.Api.csproj", "src/gateway/CloudStreams.Gateway.Api/"]
COPY ["src/gateway/CloudStreams.Gateway.Api.Client/CloudStreams.Gateway.Api.Client.csproj", "src/gateway/CloudStreams.Gateway.Api.Client/"]
COPY ["src/core/CloudStreams.Core/CloudStreams.Core.csproj", "src/core/CloudStreams.Core/"]
COPY ["src/gateway/CloudStreams.Gateway.Application/CloudStreams.Gateway.Application.csproj", "src/gateway/CloudStreams.Gateway.Application/"]
COPY ["src/core/CloudStreams.Core.Application/CloudStreams.Core.Application.csproj", "src/core/CloudStreams.Core.Application/"]
COPY ["src/core/CloudStreams.Core.Infrastructure/CloudStreams.Core.Infrastructure.csproj", "src/core/CloudStreams.Core.Infrastructure/"]
RUN dotnet restore "src/gateway/CloudStreams.Gateway.Api.Server/CloudStreams.Gateway.Api.Server.csproj"
COPY . .
WORKDIR "/src/src/gateway/CloudStreams.Gateway.Api.Server"
RUN dotnet build "CloudStreams.Gateway.Api.Server.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "CloudStreams.Gateway.Api.Server.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "CloudStreams.Gateway.Api.Server.dll"]