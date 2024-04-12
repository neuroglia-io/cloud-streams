using CloudStreams.Core.Api.Services;
using CloudStreams.Core.Application.Services;

namespace CloudStreams.Core.Api;

/// <summary>
/// Defines extensions for <see cref="ICloudStreamsApplicationBuilder"/>s
/// </summary>
public static class ICloudStreamsApiBuilderExtensions
{

    /// <summary>
    /// Configures CloudStreams to use the Core API
    /// </summary>
    /// <param name="builder">The <see cref="ICloudStreamsApplicationBuilder"/> to configure</param>
    /// <returns>The configured <see cref="ICloudStreamsApplicationBuilder"/></returns>
    public static ICloudStreamsApplicationBuilder UseCoreApi(this ICloudStreamsApplicationBuilder builder)
    {
        builder.Services.AddSignalR();
        builder.Services.AddSingleton<ResourceWatchEventHubController>();
        builder.Services.AddSingleton<IHostedService>(provider => provider.GetRequiredService<ResourceWatchEventHubController>());
        builder.Services.AddHostedService<GatewayResourceController>();
        builder.Services.AddHostedService<BrokerResourceController>();
        return builder;
    }

}

