namespace CloudStreams.Core.Infrastructure.Services;

/// <summary>
/// Defines the fundamentals of a service used to store <see cref="CloudEvent"/>s
/// </summary>
public interface ICloudEventStore
{

    /// <summary>
    /// Appends and persists the specified <see cref="CloudEvent"/> to the store
    /// </summary>
    /// <param name="e">The <see cref="CloudEvent"/> to append</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The offset of the appended <see cref="CloudEvent"/></returns>
    Task<ulong> AppendAsync(CloudEvent e, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the metadata used to describe the application's <see cref="CloudEvent"/> stream
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="CloudEventStreamMetadata"/> used to describe the <see cref="CloudEvent"/> stream</returns>
    Task<CloudEventStreamMetadata> GetStreamMetadataAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the metadata used to describe the specified <see cref="CloudEvent"/> partition
    /// </summary>
    /// <param name="partition">An object used to reference the <see cref="CloudEvent"/> partition to get the metadata of</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="CloudEventPartitionMetadata"/> used to describe the <see cref="CloudEvent"/> partition</returns>
    Task<CloudEventPartitionMetadata> GetPartitionMetadataAsync(CloudEventPartitionRef partition, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the metadata of <see cref="CloudEvent"/> partitions of the specified type
    /// </summary>
    /// <param name="partitionType">The type of partition to list</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IAsyncEnumerable{T}"/> that contains metadata that describes the partitions of the specified type</returns>
    IAsyncEnumerable<CloudEventPartitionMetadata> ListPartitionsMetadataAsync(CloudEventPartitionType partitionType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads stored <see cref="CloudEvent"/>s
    /// </summary>
    /// <param name="readDirection">The direction in which to read the stream</param>
    /// <param name="offset">The offset starting from which to read events</param>
    /// <param name="length">The amount of <see cref="CloudEvent"/>s to read</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IAsyncEnumerable{T}"/> containing the <see cref="CloudEvent"/>s read from the store</returns>
    IAsyncEnumerable<CloudEvent> ReadAsync(StreamReadDirection readDirection, long offset, ulong? length = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads stored <see cref="CloudEvent"/>s
    /// </summary>
    /// <param name="partition">An object used to reference the partition to read</param>
    /// <param name="readDirection">The direction in which to read the partition</param>
    /// <param name="offset">The offset starting from which to read events</param>
    /// <param name="length">The amount of <see cref="CloudEvent"/>s to read</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IAsyncEnumerable{T}"/> containing the <see cref="CloudEvent"/>s read from the store</returns>
    IAsyncEnumerable<CloudEvent> ReadPartitionAsync(CloudEventPartitionRef partition, StreamReadDirection readDirection, long offset, ulong? length = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to <see cref="CloudEvent"/>s
    /// </summary>
    /// <param name="offset">The offset starting from which to receive <see cref="CloudEvent"/>s. Defaults to <see cref="CloudEventStreamPosition.End"/></param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IObservable{T}"/> used to observe <see cref="CloudEvent"/>s</returns>
    Task<IObservable<CloudEvent>> SubscribeAsync(long offset = CloudEventStreamPosition.End, CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to <see cref="CloudEvent"/>s
    /// </summary>
    /// <param name="partition">An object used to reference the partition to subscribe to the events of</param>
    /// <param name="offset">The offset starting from which to receive <see cref="CloudEvent"/>s. Defaults to <see cref="CloudEventStreamPosition.End"/></param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IObservable{T}"/> used to observe <see cref="CloudEvent"/>s of the specified partition</returns>
    Task<IObservable<CloudEvent>> SubscribeToPartitionAsync(CloudEventPartitionRef partition, long offset = CloudEventStreamPosition.End, CancellationToken cancellationToken = default);

    /// <summary>
    /// Truncates stored <see cref="CloudEvent"/>s
    /// </summary>
    /// <param name="beforeVersion">The version before which to truncate <see cref="CloudEvent"/>s</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    Task TruncateAsync(long beforeVersion, CancellationToken cancellationToken = default);

}