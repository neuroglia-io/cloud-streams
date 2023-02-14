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
    /// <param name="readDirection">The direction in which to read</param>
    /// <param name="offset">The offset starting from which to read events</param>
    /// <param name="amount">The amount of <see cref="CloudEvent"/>s to read</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IAsyncEnumerable{T}"/> containing the <see cref="CloudEvent"/>s read from the store</returns>
    IAsyncEnumerable<CloudEvent> ReadAsync(StreamReadDirection readDirection, long offset, ulong? amount = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads stored <see cref="CloudEvent"/>s by source
    /// </summary>
    /// <param name="readDirection">The direction in which to read</param>
    /// <param name="source">The source of the <see cref="CloudEvent"/>s to read</param>
    /// <param name="offset">The offset starting from which to read events</param>
    /// <param name="amount">The amount of <see cref="CloudEvent"/>s to read</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IAsyncEnumerable{T}"/> containing the <see cref="CloudEvent"/>s read from the store
    IAsyncEnumerable<CloudEvent> ReadBySourceAsync(StreamReadDirection readDirection, Uri source, long offset, ulong? amount = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads stored <see cref="CloudEvent"/>s by type
    /// </summary>
    /// <param name="readDirection">The direction in which to read</param>
    /// <param name="type">The type of the <see cref="CloudEvent"/>s to read</param>
    /// <param name="offset">The offset starting from which to read events</param>
    /// <param name="amount">The amount of <see cref="CloudEvent"/>s to read</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IAsyncEnumerable{T}"/> containing the <see cref="CloudEvent"/>s read from the store
    IAsyncEnumerable<CloudEvent> ReadByTypeAsync(StreamReadDirection readDirection, string type, long offset, ulong? amount = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads stored <see cref="CloudEvent"/>s by correlation id
    /// </summary>
    /// <param name="readDirection">The direction in which to read</param>
    /// <param name="correlationId">The correlation id of the <see cref="CloudEvent"/>s to read</param>
    /// <param name="offset">The offset starting from which to read events</param>
    /// <param name="amount">The amount of <see cref="CloudEvent"/>s to read</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IAsyncEnumerable{T}"/> containing the <see cref="CloudEvent"/>s read from the store
    IAsyncEnumerable<CloudEvent> ReadByCorrelationIdAsync(StreamReadDirection readDirection, string correlationId, long offset, ulong? amount = null, CancellationToken cancellationToken = default);

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