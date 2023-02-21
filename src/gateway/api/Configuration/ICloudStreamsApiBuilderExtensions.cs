using CloudStreams.Core.Infrastructure.Configuration;
using CloudStreams.Gateway.Api.Controllers;
using CloudStreams.Gateway.Application.Commands.CloudEvents;
using CloudStreams.Gateway.Application.Configuration;
using CloudStreams.Gateway.Application.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CloudStreams.Gateway.Api.Configuration;

/// <summary>
/// Defines extensions for <see cref="ICloudStreamsApplicationBuilder"/>s
/// </summary>
public static class ICloudStreamsApiBuilderExtensions
{

    /// <summary>
    /// Configures CloudStreams to use the Gateway API
    /// </summary>
    /// <param name="builder">The <see cref="ICloudStreamsApplicationBuilder"/> to configure</param>
    /// <returns>The configured <see cref="ICloudStreamsApplicationBuilder"/></returns>
    public static ICloudStreamsApplicationBuilder UseGatewayApi(this ICloudStreamsApplicationBuilder builder)
    {
        builder.Configuration.AddEnvironmentVariables(GatewayOptions.EnvironmentVariablePrefix);
        builder.Services.Configure<GatewayOptions>(builder.Configuration);
        builder.RegisterApplicationPart<CloudEventsController>();
        builder.RegisterMediationAssembly<ConsumeEventCommand>();
        builder.Services.TryAddSingleton<CloudEventAdmissionControl>();
        builder.Services.TryAddSingleton<ICloudEventAdmissionControl>(provider => provider.GetRequiredService<CloudEventAdmissionControl>());
        builder.Services.AddHostedService(provider => provider.GetRequiredService<CloudEventAdmissionControl>());
        return builder;
    }

}
