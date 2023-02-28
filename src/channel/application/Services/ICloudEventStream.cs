using CloudStreams.Core.Data.Models;

namespace CloudStreams.Channel.Application.Services;

/// <summary>
/// Defines the fundamentals of a service used to stream inbound <see cref="CloudEvent"/>s
/// </summary>
public interface ICloudEventStream
    : IObservable<CloudEvent>
{

    /// <summary>
    /// Appends the specified inbound <see cref="CloudEvent"/> to the stream
    /// </summary>
    /// <param name="e">The <see cref="CloudEvent"/> to add</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    Task AppendAsync(CloudEvent e, CancellationToken cancellationToken = default);

}
