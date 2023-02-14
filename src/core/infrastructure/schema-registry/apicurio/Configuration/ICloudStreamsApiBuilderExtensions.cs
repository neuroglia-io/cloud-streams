using CloudStreams.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Neuroglia.Data.SchemaRegistry;

namespace CloudStreams.Infrastructure.Configuration;

/// <summary>
/// Defines extensions for <see cref="ICloudStreamsApiBuilder"/>s
/// </summary>
public static class ICloudStreamsApiBuilderExtensions
{

    private const string ConnectionStringName = "apicurio";

    /// <summary>
    /// Configures Cloud Streams to use the <see href="https://www.apicur.io/registry/">Apicurio Registry</see> implementation of the <see cref="ISchemaRegistry"/> interface
    /// </summary>
    /// <param name="builder">The <see cref="ICloudStreamsApiBuilder"/> to configure</param>
    /// <returns>The configured <see cref="ICloudStreamsApiBuilder"/></returns>
    public static ICloudStreamsApiBuilder UseApicurioSchemaRegistry(this ICloudStreamsApiBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString)) throw new Exception($"Failed to find the '{ConnectionStringName}' connection string");
        builder.Services.AddApiCurioRegistryClient(builder.Configuration, options =>
        {
            options.ServerUri = new(connectionString, UriKind.RelativeOrAbsolute);
            options.LineEndingFormatMode = LineEndingFormatMode.ConvertToUnix;
        });
        builder.Services.TryAddSingleton<ApiCurioSchemaRegistry>();
        builder.UseSchemaRegistry<ApiCurioSchemaRegistry>();
        return builder;
    }

}
