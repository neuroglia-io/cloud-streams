namespace CloudStreams.Infrastructure.Services;

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
    /// Subscribes to consumed <see cref="CloudEvent"/>s
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The id of the newly created subscription</returns>
    Task<string> SubscribeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Truncates stored <see cref="CloudEvent"/>s
    /// </summary>
    /// <param name="beforeVersion">The version before which to truncate <see cref="CloudEvent"/>s</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    Task TruncateAsync(long beforeVersion, CancellationToken cancellationToken = default);

}