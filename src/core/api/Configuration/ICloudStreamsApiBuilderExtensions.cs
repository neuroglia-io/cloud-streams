using CloudStreams.Core.Application.Queries.Streams;
using CloudStreams.Core.Infrastructure.Configuration;
using CloudStreams.Core.Api.Controllers;

namespace CloudStreams.Core.Api.Configuration;

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
    public static ICloudStreamsApplicationBuilder UseCoreApi(this ICloudStreamsApplicationBuilder builder)
    {
        builder.RegisterApplicationPart<CloudEventStreamController>();
        builder.RegisterApplicationPart<CloudEventStreamController>();
        builder.RegisterMediationAssembly<ReadEventStreamQuery>();
        return builder;
    }

}
