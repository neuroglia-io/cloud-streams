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
    public const string PartitionBySource = "cloud-events-by_source";
    /// <summary>
    /// Gets the name of the projection used to list all <see cref="CloudEvent"/> types
    /// </summary>
    public const string ListCloudEventTypes = "cloud-events-types";
    /// <summary>
    /// Gets the name of the projection used to list all <see cref="CloudEvent"/> subjects
    /// </summary>
    public const string ListCloudEventSubjects = "cloud-events-subjects";

    /// <summary>
    /// Exposes constants about EventStore built-in projections
    /// </summary>
    public static class BuiltInProjections
    {

        /// <summary>
        /// Gets the name of the built-in projection used to partition events by category
        /// </summary>
        public const string PartitionByCategory = "$by_category";
        /// <summary>
        /// Gets the name of the built-in projection used to partition events by type
        /// </summary>
        public const string PartitionByEventType = "$by_event_type";
        /// <summary>
        /// Gets the name of the built-in projection used to partition events by correlation id
        /// </summary>
        public const string PartitionByCorrelationId = "$by_correlation_id";
        /// <summary>
        /// Gets the name of the built-in projection used to collect stream-related events
        /// </summary>
        public const string Streams = "$streams";
        /// <summary>
        /// Gets the name of the built-in projection used to collect stream-related events by category
        /// </summary>
        public const string StreamByCategory = "$stream_by_category";

    }

}
