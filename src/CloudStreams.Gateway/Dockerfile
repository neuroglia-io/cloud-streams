#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER app
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/gateway/CloudStreams.Gateway/CloudStreams.Gateway.csproj", "src/gateway/CloudStreams.Gateway/"]
RUN dotnet restore "./src/gateway/CloudStreams.Gateway/CloudStreams.Gateway.csproj"
COPY . .
WORKDIR "/src/src/gateway/CloudStreams.Gateway"
RUN dotnet build "./CloudStreams.Gateway.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./CloudStreams.Gateway.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "CloudStreams.Gateway.dll"]