FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER app
WORKDIR /app
EXPOSE 8080
EXPOSE 8443

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/operator/CloudStreams.Operator/CloudStreams.Operator.csproj", "src/operator/CloudStreams.Operator/"]
RUN dotnet restore "./src/operator/CloudStreams.Operator/CloudStreams.Operator.csproj"
COPY . .
WORKDIR "/src/src/operator/CloudStreams.Operator"
RUN dotnet build "./CloudStreams.Operator.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./CloudStreams.Operator.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "CloudStreams.Operator.dll"]
