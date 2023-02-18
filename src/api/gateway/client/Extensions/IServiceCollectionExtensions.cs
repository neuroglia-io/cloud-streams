using CloudStreams.Api.Gateway.Client.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CloudStreams.Api.Gateway.Client;

/// <summary>
/// Defines extensions for <see cref="IServiceCollection"/>s
/// </summary>
public static class IServiceCollectionExtensions
{

    /// <summary>
    /// Adds and configures a new <see cref="ICloudStreamsGatewayApiClient"/>
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to configure</param>
    /// <returns>The configured <see cref="IServiceCollection"/></returns>
    public static IServiceCollection AddCloudStreamsGatewayApiClient(this IServiceCollection services)
    {
        services.TryAddScoped<ICloudStreamsGatewayApiClient, CloudStreamsGatewayApiClient>();
        return services;
    }

}
