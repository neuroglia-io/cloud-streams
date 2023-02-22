using CloudStreams.Gateway.Api.Client.Configuration;
using CloudStreams.Gateway.Api.Client.Services;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace CloudStreams.Gateway.Api.Client;

/// <summary>
/// Defines extensions for <see cref="IServiceCollection"/>s
/// </summary>
public static class IServiceCollectionExtensions
{

    /// <summary>
    /// Adds and configures a new <see cref="ICloudStreamsGatewayApiClient"/>
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to configure</param>
    /// <param name="setup">An <see cref="Action{T}"/> used to setup the <see cref="CloudStreamGatewayApiClientOptions"/> to use</param>
    /// <returns>The configured <see cref="IServiceCollection"/></returns>
    public static IServiceCollection AddCloudStreamsGatewayApiClient(this IServiceCollection services, Action<CloudStreamGatewayApiClientOptions> setup)
    {
        services.Configure(setup);
        services.TryAddScoped<ICloudStreamsGatewayApiClient, CloudStreamsGatewayApiClient>();
        services.TryAddSingleton(provider =>
        {
            var options = provider.GetRequiredService<IOptions<CloudStreamGatewayApiClientOptions>>().Value;
            var connection = new HubConnectionBuilder()
                .WithUrl($"{options.BaseAddress}api/v1/ws/cloud-events")
                .WithAutomaticReconnect()
                .Build();
            return new CloudEventHubClient(connection);
        });
        return services;
    }

}