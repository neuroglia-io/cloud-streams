using CloudStreams.Core.Data.Models;

namespace CloudStreams.Gateway.Api.Client.Services;

/// <summary>
/// Defines the fundamentals of the Cloud Streams gateway API used to manage <see cref="CloudEvent"/>s
/// </summary>
public interface ICloudEventsApi
{

    /// <summary>
    /// Publishes the specified <see cref="CloudEvent"/> to the Cloud Streams gateway
    /// </summary>
    /// <param name="e">The <see cref="CloudEvent"/> to publish</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    Task PublishCloudEventAsync(CloudEvent e, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the API used to manage <see cref="CloudEvent"/> partitions
    /// </summary>
    ICloudEventPartitionsApi Partitions { get; }

    /// <summary>
    /// Gets the API used to manage the gateway's <see cref="CloudEvent"/> stream
    /// </summary>
    ICloudEventStreamApi Stream { get; }

}
