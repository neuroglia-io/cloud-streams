using CloudStreams.Data.Models;

namespace CloudStreams.Infrastructure;

/// <summary>
/// Exposes constants about the EventStore projections used by Cloud Streams
/// </summary>
public static class EventStoreProjections
{

    /// <summary>
    /// Gets the name of the projection used to partition <see cref="CloudEvent"/>s by source
    /// </summary>
    public const string ByCloudEventSource = "cse_by_source";

    /// <summary>
    /// Gets the name of the built-in projection used to collect stream-related events
    /// </summary>
    public const string ByStreams = "$streams";

}
