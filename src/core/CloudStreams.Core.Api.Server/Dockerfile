#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 80
RUN apt-get update
RUN apt-get install -y jq

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["src/core/CloudStreams.Core.Api.Server/CloudStreams.Core.Api.Server.csproj", "src/core/CloudStreams.Core.Api.Server/"]
COPY ["src/dashboard/CloudStreams.Dashboard/CloudStreams.Dashboard.csproj", "src/dashboard/CloudStreams.Dashboard/"]
COPY ["src/core/CloudStreams.Core.Api.Client/CloudStreams.Core.Api.Client.csproj", "src/core/CloudStreams.Core.Api.Client/"]
COPY ["src/core/CloudStreams.Core/CloudStreams.Core.csproj", "src/core/CloudStreams.Core/"]
COPY ["src/dashboard/CloudStreams.Dashboard.StateManagement/CloudStreams.Dashboard.StateManagement.csproj", "src/dashboard/CloudStreams.Dashboard.StateManagement/"]
COPY ["src/core/CloudStreams.Core.Api/CloudStreams.Core.Api.csproj", "src/core/CloudStreams.Core.Api/"]
COPY ["src/core/CloudStreams.Core.Application/CloudStreams.Core.Application.csproj", "src/core/CloudStreams.Core.Application/"]
COPY ["src/core/CloudStreams.Core.Infrastructure/CloudStreams.Core.Infrastructure.csproj", "src/core/CloudStreams.Core.Infrastructure/"]
RUN dotnet restore "src/core/CloudStreams.Core.Api.Server/CloudStreams.Core.Api.Server.csproj"
COPY . .
WORKDIR "/src/src/core/CloudStreams.Core.Api.Server"
RUN dotnet build "CloudStreams.Core.Api.Server.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "CloudStreams.Core.Api.Server.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "CloudStreams.Core.Api.Server.dll"]