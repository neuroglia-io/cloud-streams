using CloudStreams.Broker.Application.Configuration;
using CloudStreams.Broker.Application.Services;
using CloudStreams.Core.Infrastructure.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CloudStreams.Broker.Api.Configuration;

/// <summary>
/// Defines extensions for <see cref="ICloudStreamsApplicationBuilder"/>s
/// </summary>
public static class ICloudStreamsApiBuilderExtensions
{

    /// <summary>
    /// Configures CloudStreams to use the Broker API
    /// </summary>
    /// <param name="builder">The <see cref="ICloudStreamsApplicationBuilder"/> to configure</param>
    /// <returns>The configured <see cref="ICloudStreamsApplicationBuilder"/></returns>
    public static ICloudStreamsApplicationBuilder UseBrokerApi(this ICloudStreamsApplicationBuilder builder)
    {
        builder.Configuration.AddEnvironmentVariables(BrokerOptions.EnvironmentVariablePrefix);
        builder.Services.Configure<BrokerOptions>(builder.Configuration);
        //builder.RegisterApplicationPart<CloudEventsController>();
        builder.Services.AddResourceController<Channel>();
        builder.Services.TryAddSingleton<BrokerCloudEventDispatcher>();
        builder.Services.AddHostedService(provider => provider.GetRequiredService<BrokerCloudEventDispatcher>());
        return builder;
    }

}
