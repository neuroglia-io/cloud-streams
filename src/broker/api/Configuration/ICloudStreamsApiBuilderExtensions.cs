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
        var options = new BrokerOptions();
        builder.Configuration.AddEnvironmentVariables(BrokerOptions.EnvironmentVariablePrefix);
        builder.Configuration.Bind(options);

        builder.WithServiceName(options.Name);
        builder.Services.Configure<BrokerOptions>(builder.Configuration);
        builder.Services.AddResourceController<Subscription>();
        builder.Services.TryAddSingleton<SubscriptionManager>();
        builder.Services.AddHostedService(provider => provider.GetRequiredService<SubscriptionManager>());
        return builder;
    }

}
