using CloudStreams.Core.Infrastructure.Configuration;
using CloudStreams.Gateway.Api.Controllers;
using CloudStreams.Gateway.Application.Commands.CloudEvents;

namespace CloudStreams.Api.Configuration;

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
        builder.RegisterApplicationPart<CloudEventsController>();
        builder.RegisterMediationAssembly<ConsumeEventCommand>();
        return builder;
    }

}
