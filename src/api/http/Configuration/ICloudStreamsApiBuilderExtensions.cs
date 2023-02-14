using CloudStreams.Api.Http.Formatters;
using CloudStreams.Infrastructure.Configuration;
using Microsoft.AspNetCore.ResponseCompression;

namespace CloudStreams.Api.Configuration;

/// <summary>
/// Defines extensions for <see cref="ICloudStreamsApiBuilder"/>s
/// </summary>
public static class ICloudStreamsApiBuilderExtensions
{

    /// <summary>
    /// Configures the <see cref="ICloudStreamsApiBuilder"/> to serve HTTP endpoints
    /// </summary>
    /// <param name="builder">The <see cref="ICloudStreamsApiBuilder"/> to configure</param>
    /// <returns>The configured <see cref="ICloudStreamsApiBuilder"/></returns>
    public static ICloudStreamsApiBuilder UseHttpEndpoints(this ICloudStreamsApiBuilder builder)
    {
        builder.Services.AddHealthChecks();
        builder.Services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.Providers.Add<BrotliCompressionProvider>();
            options.Providers.Add<GzipCompressionProvider>();
        });
        builder.Services.AddProblemDetails();
        builder.Services.AddControllers(options =>
        {
            options.InputFormatters.Add(new YamlInputFormatter());
            options.OutputFormatters.Add(new YamlOutputFormatter());
        })
            .AddJsonOptions(options =>
            {
                Serializer.Json.DefaultOptionsConfiguration?.Invoke(options.JsonSerializerOptions);
            })
            .AddApplicationPart(typeof(ICloudStreamsApiBuilderExtensions).Assembly);
        builder.Services.AddEndpointsApiExplorer();
        return builder;
    }

}
