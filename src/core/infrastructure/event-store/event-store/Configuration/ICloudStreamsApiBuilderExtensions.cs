using Microsoft.Extensions.Configuration;

namespace CloudStreams.Infrastructure.Configuration;

/// <summary>
/// Defines extensions for <see cref="ICloudStreamsApiBuilder"/>s
/// </summary>
public static class ICloudStreamsApiBuilderExtensions
{

    private const string ConnectionStringName = "eventstore";

    /// <summary>
    /// Configures Cloud Streams to use the <see href="https://www.eventstore.com/">EventStore</see> implementation of the <see cref="ICloudEventStore"/> interface
    /// </summary>
    /// <param name="builder">The <see cref="ICloudStreamsApiBuilder"/> to configure</param>
    /// <returns>The configured <see cref="ICloudStreamsApiBuilder"/></returns>
    public static ICloudStreamsApiBuilder UseESCloudEventStore(this ICloudStreamsApiBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString)) throw new Exception($"Failed to find the '{ConnectionStringName}' connection string");
        builder.Services.AddEventStore(EventStoreClientSettings.Create(connectionString));
        builder.Services.TryAddSingleton<ESCloudEventStore>();
        builder.Services.AddSingleton<IHostedService>(provider => provider.GetRequiredService<ESCloudEventStore>());
        builder.UseCloudEventStore<ESCloudEventStore>();
        return builder;
    }

}
