FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER app
WORKDIR /app
EXPOSE 80
USER root
RUN apt-get update
RUN apt-get install -y jq

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/core/CloudStreams.Core.Api/CloudStreams.Core.Api.csproj", "src/core/CloudStreams.Core.Api/"]
COPY ["src/core/CloudStreams.Core.Api.Client/CloudStreams.Core.Api.Client.csproj", "src/core/CloudStreams.Core.Api.Client/"]
COPY ["src/core/CloudStreams.Core/CloudStreams.Core.csproj", "src/core/CloudStreams.Core/"]
COPY ["src/core/CloudStreams.Core.Application/CloudStreams.Core.Application.csproj", "src/core/CloudStreams.Core.Application/"]
RUN dotnet restore "./src/core/CloudStreams.Core.Api/CloudStreams.Core.Api.csproj"
COPY . .
WORKDIR "/src/src/core/CloudStreams.Core.Api"
RUN dotnet build "./CloudStreams.Core.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./CloudStreams.Core.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "CloudStreams.Core.Api.dll"]