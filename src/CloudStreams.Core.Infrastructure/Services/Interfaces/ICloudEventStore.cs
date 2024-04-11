using CloudStreams.Core.Resources;
using Neuroglia.Data.Infrastructure.EventSourcing;
using Neuroglia.Eventing.CloudEvents;

namespace CloudStreams.Core.Infrastructure.Services;

/// <summary>
/// Defines the fundamentals of a service used to source <see cref="CloudEvent"/>s
/// </summary>
public interface ICloudEventStore
{

    /// <summary>
    /// Appends and persists the specified <see cref="CloudEvent"/> to the store
    /// </summary>
    /// <param name="e">An object that describes the <see cref="CloudEvent"/> to append</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>An object that describes the recorded <see cref="CloudEvent"/></returns>
    Task<CloudEventRecord> AppendAsync(CloudEventDescriptor e, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the metadata used to describe the application's <see cref="CloudEvent"/> stream
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="StreamMetadata"/> used to describe the <see cref="CloudEvent"/> stream</returns>
    Task<StreamMetadata> GetStreamMetadataAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the metadata used to describe the specified <see cref="CloudEvent"/> partition
    /// </summary>
    /// <param name="partition">An object used to reference the <see cref="CloudEvent"/> partition to get the metadata of</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="PartitionMetadata"/> used to describe the <see cref="CloudEvent"/> partition</returns>
    Task<PartitionMetadata> GetPartitionMetadataAsync(PartitionReference partition, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the <see cref="CloudEvent"/> partition ids of the specified type
    /// </summary>
    /// <param name="partitionType">The type of partition to list</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IAsyncEnumerable{T}"/> that contains ids of the partitions of the specified type</returns>
    IAsyncEnumerable<string> ListPartitionIdsAsync(CloudEventPartitionType partitionType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads recorded <see cref="CloudEvent"/>s
    /// </summary>
    /// <param name="readDirection">The direction in which to read the stream</param>
    /// <param name="offset">The offset starting from which to read events</param>
    /// <param name="length">The amount of <see cref="CloudEvent"/>s to read</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IAsyncEnumerable{T}"/> containing the <see cref="CloudEvent"/>s read from the store</returns>
    IAsyncEnumerable<CloudEventRecord> ReadAsync(StreamReadDirection readDirection, long offset, ulong? length = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads recorded <see cref="CloudEvent"/>s
    /// </summary>
    /// <param name="partition">An object used to reference the partition to read</param>
    /// <param name="readDirection">The direction in which to read the partition</param>
    /// <param name="offset">The offset starting from which to read events</param>
    /// <param name="length">The amount of <see cref="CloudEvent"/>s to read</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IAsyncEnumerable{T}"/> containing the <see cref="CloudEvent"/>s read from the store</returns>
    IAsyncEnumerable<CloudEventRecord> ReadPartitionAsync(PartitionReference partition, StreamReadDirection readDirection, long offset, ulong? length = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to <see cref="CloudEvent"/>s
    /// </summary>
    /// <param name="offset">The offset starting from which to receive <see cref="CloudEvent"/>s. Defaults to <see cref="StreamPosition.EndOfStream"/></param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IObservable{T}"/> used to observe <see cref="CloudEvent"/>s</returns>
    Task<IObservable<CloudEventRecord>> ObserveAsync(long offset = StreamPosition.EndOfStream, CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to <see cref="CloudEvent"/>s
    /// </summary>
    /// <param name="partition">An object used to reference the partition to subscribe to the events of</param>
    /// <param name="offset">The offset starting from which to receive <see cref="CloudEvent"/>s. Defaults to <see cref="StreamPosition.EndOfStream"/></param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IObservable{T}"/> used to observe <see cref="CloudEvent"/>s of the specified partition</returns>
    Task<IObservable<CloudEventRecord>> ObservePartitionAsync(PartitionReference partition, long offset = StreamPosition.EndOfStream, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Truncates stored <see cref="CloudEvent"/>s
    /// </summary>
    /// <param name="beforeVersion">The version before which to truncate <see cref="CloudEvent"/>s</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    Task TruncateAsync(ulong beforeVersion, CancellationToken cancellationToken = default);

}