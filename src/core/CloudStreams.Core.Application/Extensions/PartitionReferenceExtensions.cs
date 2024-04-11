namespace CloudStreams.Core.Application;

/// <summary>
/// Defines extensions for <see cref="PartitionReference"/>s
/// </summary>
public static class PartitionReferenceExtensions
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
            CloudEventPartitionType.BySource => Streams.ByCloudEventSource(new(partition.Id)),
            CloudEventPartitionType.BySubject => Streams.ByCloudEventSubject(partition.Id),
            CloudEventPartitionType.ByType => Streams.ByCloudEventType(partition.Id),
            CloudEventPartitionType.ByCorrelationId => Streams.ByCorrelationId(partition.Id),
            CloudEventPartitionType.ByCausationId => Streams.ByCausationId(partition.Id),
            _ => throw new NotSupportedException($"The specified partition type '{partition.Type}' is not supported")
        };
    }

}
