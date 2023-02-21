using CloudStreams.Core.Infrastructure.Services;

namespace CloudStreams.Core.Infrastructure;

/// <summary>
/// Defines extensions for <see cref="ICloudEventStore"/>s
/// </summary>
public static class ICloudEventStoreExtensions
{

    /// <summary>
    /// Reads a single event from the stream
    /// </summary>
    /// <param name="events">The <see cref="ICloudEventStore"/> to read the event from</param>
    /// <param name="direction">The direction in which to read the stream</param>
    /// <param name="offset">The offset of the event to read</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The <see cref="CloudEvent"/> at the specified offset</returns>
    public static async Task<CloudEvent?> ReadOneAsync(this ICloudEventStore events, StreamReadDirection direction, long offset, CancellationToken cancellationToken = default)
    {
        return await events.ReadAsync(direction, offset, 1, cancellationToken).FirstOrDefaultAsync().ConfigureAwait(false);
    }

}