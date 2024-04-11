using CloudStreams.Core;

namespace CloudStreams;

/// <summary>
/// Exposes constants about the event sourcing streams used by Cloud Streams
/// </summary>
public static class Streams
{

    /// <summary>
    /// Gets the name of the stream that contains all <see cref="Neuroglia.Eventing.CloudEvents.CloudEvent"/>s
    /// </summary>
    public const string All = "cloud-events";

    /// <summary>
    /// Gets the prefix of the names of all <see cref="CloudEventPartitionType.BySource"/> partition EventStore streams
    /// </summary>
    public const string ByCloudEventSourcePrefix = "$by-source-";
    /// <summary>
    /// Gets the prefix of the names of all <see cref="CloudEventPartitionType.BySubject"/> partition EventStore streams
    /// </summary>
    public const string ByCloudEventSubjectPrefix = "$by-subject-";
    /// <summary>
    /// Gets the prefix of the names of all <see cref="CloudEventPartitionType.ByType"/> partition EventStore streams
    /// </summary>
    public const string ByCloudEventTypePrefix = "$et-";
    /// <summary>
    /// Gets the prefix of the names of all <see cref="CloudEventPartitionType.ByCorrelationId"/> partition EventStore streams
    /// </summary>
    public const string ByCorrelationIdPrefix = "$bc-";
    /// <summary>
    /// Gets the prefix of the names of all <see cref="CloudEventPartitionType.ByCausationId"/> partition EventStore streams
    /// </summary>
    public const string ByCausationIdPrefix = "$by-causation-";

    /// <summary>
    /// Gets the name of the stream that partitions <see cref="Neuroglia.Eventing.CloudEvents.CloudEvent"/>s by source
    /// </summary>
    public static string ByCloudEventSource(Uri source) => $"{ByCloudEventSourcePrefix}{source.OriginalString}";
    /// <summary>
    /// Gets the name of the stream that partitions <see cref="Neuroglia.Eventing.CloudEvents.CloudEvent"/>s by subject
    /// </summary>
    public static string ByCloudEventSubject(string subject) => $"{ByCloudEventSubjectPrefix}{subject}";
    /// <summary>
    /// Gets the name of the stream that partitions <see cref="Neuroglia.Eventing.CloudEvents.CloudEvent"/>s by type
    /// </summary>
    public static string ByCloudEventType(string type) => $"{ByCloudEventTypePrefix}{type}";
    /// <summary>
    /// Gets the name of the stream that partitions <see cref="Neuroglia.Eventing.CloudEvents.CloudEvent"/>s by correlation id
    /// </summary>
    public static string ByCorrelationId(string correlationId) => $"{ByCorrelationIdPrefix}{correlationId}";
    /// <summary>
    /// Gets the name of the stream that partitions <see cref="Neuroglia.Eventing.CloudEvents.CloudEvent"/>s by causation id
    /// </summary>
    public static string ByCausationId(string causationId) => $"{ByCausationIdPrefix}{causationId}";

    /// <summary>
    /// Determines whether or not the specified EventStore stream name is the one of a partition of the specified type
    /// </summary>
    /// <param name="streamName">The EventStore stream name</param>
    /// <param name="partitionType">The partition type</param>
    /// <returns>A boolean indicating whether or not the specified EventStore stream name is the one of a partition of the specified type</returns>
    public static bool IsPartition(string streamName, CloudEventPartitionType partitionType)
    {
        if (string.IsNullOrWhiteSpace(streamName)) throw new ArgumentNullException(nameof(streamName));
        return partitionType switch
        {
            CloudEventPartitionType.BySource => streamName.StartsWith(ByCloudEventSourcePrefix),
            CloudEventPartitionType.BySubject => streamName.StartsWith(ByCloudEventSubjectPrefix),
            CloudEventPartitionType.ByType => streamName.StartsWith(ByCloudEventTypePrefix),
            CloudEventPartitionType.ByCorrelationId => streamName.StartsWith(ByCorrelationIdPrefix),
            CloudEventPartitionType.ByCausationId => streamName.StartsWith(ByCausationIdPrefix),
            _ => throw new NotSupportedException($"The specified {nameof(CloudEventPartitionType)} '{partitionType}' is not supported")
        };
    }

    /// <summary>
    /// Extracts the id of a partition of the specified type from an EventStore stream name
    /// </summary>
    /// <param name="streamName">The EventStore stream name to extract the id from</param>
    /// <param name="partitionType">The type of partition ot get the id of</param>
    /// <returns>The id of the specified partition</returns>
    public static string ExtractPartitionIdFrom(string streamName, CloudEventPartitionType partitionType)
    {
        if (string.IsNullOrWhiteSpace(streamName)) throw new ArgumentNullException(nameof(streamName));
        return partitionType switch
        {
            CloudEventPartitionType.BySource => streamName[ByCloudEventSourcePrefix.Length..],
            CloudEventPartitionType.BySubject => streamName[ByCloudEventSubjectPrefix.Length..],
            CloudEventPartitionType.ByType => streamName[ByCloudEventTypePrefix.Length..],
            CloudEventPartitionType.ByCorrelationId => streamName[ByCorrelationIdPrefix.Length..],
            CloudEventPartitionType.ByCausationId => streamName[ByCausationIdPrefix.Length..],
            _ => throw new NotSupportedException($"The specified {nameof(CloudEventPartitionType)} '{partitionType}' is not supported")
        };
    }

}
