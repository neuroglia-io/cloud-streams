using CloudStreams.ResourceManagement.Api.Client.Configuration;
using CloudStreams.ResourceManagement.Api.Client.Services;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace CloudStreams.ResourceManagement.Api.Client;

/// <summary>
/// Defines extensions for <see cref="IServiceCollection"/>s
/// </summary>
public static class IServiceCollectionExtensions
{

    /// <summary>
    /// Adds and configures a new <see cref="ICloudStreamsResourceManagementApiClient"/>
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to configure</param>
    /// <param name="setup">An <see cref="Action{T}"/> used to setup the <see cref="CloudStreamResourceManagementApiClientOptions"/> to use</param>
    /// <returns>The configured <see cref="IServiceCollection"/></returns>
    public static IServiceCollection AddCloudStreamsResourceManagementApiClient(this IServiceCollection services, Action<CloudStreamResourceManagementApiClientOptions> setup)
    {
        services.Configure(setup);
        services.TryAddScoped<ICloudStreamsResourceManagementApiClient, CloudStreamsResourceManagementApiClient>();
        services.TryAddSingleton(provider =>
        {
            var options = provider.GetRequiredService<IOptions<CloudStreamResourceManagementApiClientOptions>>().Value;
            var connection = new HubConnectionBuilder()
                .WithUrl($"{options.BaseAddress}api/resource-management/v1/ws/cloud-events")
                .WithAutomaticReconnect()
                .Build();
            return new ResourceEventHubClient(connection);
        });
        return services;
    }

}
