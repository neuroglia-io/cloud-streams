#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["CloudStreams.Api.Server/CloudStreams.Api.Server.csproj", "CloudStreams.Api.Server/"]
RUN dotnet restore "CloudStreams.Api.Server/CloudStreams.Api.Server.csproj"
COPY . .
WORKDIR "/src/CloudStreams.Api.Server"
RUN dotnet build "CloudStreams.Api.Server.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "CloudStreams.Api.Server.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "CloudStreams.Api.Server.dll"]