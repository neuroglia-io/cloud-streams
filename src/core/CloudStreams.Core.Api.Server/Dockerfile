#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 80
RUN apt-get update
RUN apt-get install -y jq

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["src/core/CloudStreams.Core.Api.Server/CloudStreams.Core.Api.Server.csproj", "src/core/CloudStreams.Core.Api.Server/"]
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