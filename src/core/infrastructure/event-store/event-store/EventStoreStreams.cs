namespace CloudStreams.Infrastructure;

/// <summary>
/// Exposes constants about the EventStore streams used by Cloud Streams
/// </summary>
public static class EventStoreStreams
{

    /// <summary>
    /// Gets the name of the stream that contains all <see cref="CloudEvent"/>s
    /// </summary>
    public const string All = "cse";
    /// <summary>
    /// Gets the name of the stream that partitions <see cref="CloudEvent"/>s by source
    /// </summary>
    public static string ByCloudEventSource(Uri source) => $"{All}-by_source-{source}";
    /// <summary>
    /// Gets the name of the stream that partitions <see cref="CloudEvent"/>s by type
    /// </summary>
    public static string ByCloudEventType(string type) => $"$et-{type}";
    /// <summary>
    /// Gets the name of the stream that partitions <see cref="CloudEvent"/>s by correlation id
    /// </summary>
    public static string ByCorrelationId(string correlationId) => $"bc-{correlationId}";

}
