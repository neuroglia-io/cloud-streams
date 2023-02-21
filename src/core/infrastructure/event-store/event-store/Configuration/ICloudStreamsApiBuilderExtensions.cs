using Microsoft.Extensions.Configuration;

namespace CloudStreams.Core.Infrastructure.Configuration;

/// <summary>
/// Defines extensions for <see cref="ICloudStreamsApplicationBuilder"/>s
/// </summary>
public static class ICloudStreamsApiBuilderExtensions
{

    private const string ConnectionStringName = "eventstore";

    /// <summary>
    /// Configures Cloud Streams to use the <see href="https://www.eventstore.com/">EventStore</see> implementation of the <see cref="ICloudEventStore"/> interface
    /// </summary>
    /// <param name="builder">The <see cref="ICloudStreamsApplicationBuilder"/> to configure</param>
    /// <returns>The configured <see cref="ICloudStreamsApplicationBuilder"/></returns>
    public static ICloudStreamsApplicationBuilder UseESCloudEventStore(this ICloudStreamsApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString)) throw new Exception($"Failed to find the '{ConnectionStringName}' connection string");
        builder.Services.AddEventStore(EventStoreClientSettings.Create(connectionString));
        builder.Services.TryAddSingleton<ESCloudEventStore>();
        builder.Services.AddSingleton<IHostedService>(provider => provider.GetRequiredService<ESCloudEventStore>());
        builder.RegisterHealthCheck(healthChecks => healthChecks.AddEventStore(connectionString, tags: new string[] { "cloud-event-store" }));
        builder.UseCloudEventStore<ESCloudEventStore>();
        return builder;
    }

}
