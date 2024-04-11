using Neuroglia.Eventing.CloudEvents;

namespace CloudStreams;

/// <summary>
/// Exposes constants about the EventStore projections used by Cloud Streams
/// </summary>
public static class Projections
{

    /// <summary>
    /// Gets the name of the projection used to partition <see cref="CloudEvent"/>s by source
    /// </summary>
    public const string PartitionBySource = "cloud_events_by_source";

    /// <summary>
    /// Gets the name of the projection used to partition <see cref="CloudEvent"/>s by $causationId
    /// </summary>
    public const string PartitionByCausationId = "cloud_events_by_causation_id";

    /// <summary>
    /// Gets the name of the projection used to partition <see cref="CloudEvent"/>s by subject
    /// </summary>
    public const string PartitionBySubject = "cloud_events_by_subject";

    /// <summary>
    /// Gets the prefix of name of the projection used to gather all <see cref="CloudEvent"/>'s partitions ids
    /// </summary>
    public const string CloudEventPartitionsMetadataPrefix = "cloud_events_partition_";

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