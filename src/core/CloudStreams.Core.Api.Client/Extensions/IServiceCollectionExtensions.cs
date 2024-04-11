using CloudStreams.Core.Api.Client.Services;
using CloudStreams.ResourceManagement.Api.Client.Configuration;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace CloudStreams.Core.Api.Client;

/// <summary>
/// Defines extensions for <see cref="IServiceCollection"/>s
/// </summary>
public static class IServiceCollectionExtensions
{

    /// <summary>
    /// Adds and configures a new <see cref="ICloudStreamsCoreApiClient"/>
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to configure</param>
    /// <param name="setup">An <see cref="Action{T}"/> used to setup the <see cref="CoreApiClientOptions"/> to use</param>
    /// <returns>The configured <see cref="IServiceCollection"/></returns>
    public static IServiceCollection AddCloudStreamsCoreApiClient(this IServiceCollection services, Action<CoreApiClientOptions> setup)
    {
        services.Configure(setup);
        services.TryAddScoped<ICloudStreamsCoreApiClient, CloudStreamsCoreApiClient>();
        services.TryAddSingleton(provider =>
        {
            var options = provider.GetRequiredService<IOptions<CoreApiClientOptions>>().Value;
            var connection = new HubConnectionBuilder()
                .WithUrl($"{options.BaseAddress}api/resource-management/v1/ws/watch")
                .WithAutomaticReconnect()
                .Build();
            return new ResourceWatchEventHubClient(connection);
        });
        return services;
    }

}
