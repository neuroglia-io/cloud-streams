using CloudStreams.Data.Models;

namespace CloudStreams.Infrastructure;

/// <summary>
/// Defines extensions for <see cref="CloudEventPartitionRef"/>s
/// </summary>
public static class CloudEventPartitionRefExtensions
{

    /// <summary>
    /// Gets the EventStore stream name for the specified partition
    /// </summary>
    /// <param name="partition">A reference of the partition to get the EventStore stream name for</param>
    /// <returns>The EventStore stream name for the specified <see cref="CloudEventPartitionRef"/></returns>
    public static string GetStreamName(this CloudEventPartitionRef partition)
    {
        return partition.Type switch
        {
            CloudEventPartitionType.BySource => EventStoreStreams.ByCloudEventSource(new(partition.Id)),
            CloudEventPartitionType.ByType => EventStoreStreams.ByCloudEventType(partition.Id),
            CloudEventPartitionType.BySubject => EventStoreStreams.ByCorrelationId(partition.Id),
            _ => throw new NotSupportedException($"The specified partition type '{partition.Type}' is not supported")
        };
    }

}
