FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER root
RUN apt-get update
RUN apt-get install -y jq
USER app
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/broker/CloudStreams.Broker.Api/CloudStreams.Broker.Api.csproj", "src/broker/CloudStreams.Broker.Api/"]
RUN dotnet restore "./src/broker/CloudStreams.Broker.Api/CloudStreams.Broker.Api.csproj"
COPY . .
WORKDIR "/src/src/broker/CloudStreams.Broker.Api"
RUN dotnet build "./CloudStreams.Broker.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./CloudStreams.Broker.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "CloudStreams.Broker.Api.dll"]