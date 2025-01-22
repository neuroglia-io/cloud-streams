FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER app
WORKDIR /app
EXPOSE 80
USER root
RUN apt-get update
RUN apt-get install -y jq

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/gateway/CloudStreams.Gateway.Api/CloudStreams.Gateway.Api.csproj", "src/gateway/CloudStreams.Gateway.Api/"]
COPY ["src/core/CloudStreams.Core.Api/CloudStreams.Core.Api.csproj", "src/core/CloudStreams.Core.Api/"]
COPY ["src/core/CloudStreams.Core.Application/CloudStreams.Core.Application.csproj", "src/core/CloudStreams.Core.Application/"]
COPY ["src/core/CloudStreams.Core/CloudStreams.Core.csproj", "src/core/CloudStreams.Core/"]
COPY ["src/gateway/CloudStreams.Gateway.Application/CloudStreams.Gateway.Application.csproj", "src/gateway/CloudStreams.Gateway.Application/"]
RUN dotnet restore "./src/gateway/CloudStreams.Gateway.Api/CloudStreams.Gateway.Api.csproj"
COPY . .
WORKDIR "/src/src/gateway/CloudStreams.Gateway.Api"
RUN dotnet build "./CloudStreams.Gateway.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./CloudStreams.Gateway.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "CloudStreams.Gateway.Api.dll"]