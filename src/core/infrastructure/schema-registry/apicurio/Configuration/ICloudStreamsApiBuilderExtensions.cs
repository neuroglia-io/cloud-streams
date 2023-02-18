using CloudStreams.Core.Infrastructure.SchemaRegistry.Apicurio.Configuration;
using CloudStreams.Core.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CloudStreams.Core.Infrastructure.Configuration;

/// <summary>
/// Defines extensions for <see cref="ICloudStreamsApplicationBuilder"/>s
/// </summary>
public static class ICloudStreamsApiBuilderExtensions
{

    private const string ConnectionStringName = "apicurio";

    /// <summary>
    /// Configures Cloud Streams to use the <see href="https://www.apicur.io/registry/">Apicurio Registry</see> implementation of the <see cref="ISchemaRegistry"/> interface
    /// </summary>
    /// <param name="builder">The <see cref="ICloudStreamsApplicationBuilder"/> to configure</param>
    /// <returns>The configured <see cref="ICloudStreamsApplicationBuilder"/></returns>
    public static ICloudStreamsApplicationBuilder UseApicurioSchemaRegistry(this ICloudStreamsApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString)) throw new Exception($"Failed to find the '{ConnectionStringName}' connection string");
        builder.Services.AddApiCurioRegistryClient(options =>
        {
            options.ServerUri = new(connectionString, UriKind.RelativeOrAbsolute);
            options.LineEndingFormatMode = LineEndingFormatMode.ConvertToUnix;
        });
        builder.Services.TryAddSingleton<ApiCurioSchemaRegistry>();
        builder.UseSchemaRegistry<ApiCurioSchemaRegistry>();
        return builder;
    }

}
