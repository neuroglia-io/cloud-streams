using CloudStreams.Core.Data.Models;

namespace CloudStreams.Core.Infrastructure;

/// <summary>
/// Defines extensions for <see cref="PartitionReference"/>s
/// </summary>
public static class CloudEventPartitionRefExtensions
{

    /// <summary>
    /// Gets the EventStore stream name for the specified partition
    /// </summary>
    /// <param name="partition">A reference of the partition to get the EventStore stream name for</param>
    /// <returns>The EventStore stream name for the specified <see cref="PartitionReference"/></returns>
    public static string GetStreamName(this PartitionReference partition)
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
