using CloudStreams.Core.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CloudStreams.Core.Infrastructure.Configuration;

/// <summary>
/// Defines extensions for <see cref="ICloudStreamsApplicationBuilder"/>s
/// </summary>
public static class ICloudStreamsApiBuilderExtensions
{

    /// <summary>
    /// Configures Cloud Streams to use the <see href="https://stedolan.github.io/jq/">JQ</see> implementation of the <see cref="IExpressionEvaluator"/> interface
    /// </summary>
    /// <param name="builder">The <see cref="ICloudStreamsApplicationBuilder"/> to configure</param>
    /// <returns>The configured <see cref="ICloudStreamsApplicationBuilder"/></returns>
    public static ICloudStreamsApplicationBuilder UseJQExpressionEvaluator(this ICloudStreamsApplicationBuilder builder)
    {
        builder.Services.TryAddSingleton<IExpressionEvaluator, JQExpressionEvaluator>();
        return builder;
    }

}
