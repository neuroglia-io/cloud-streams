namespace CloudStreams.Gateway.Api.Hubs;

/// <summary>
/// Defines the fundamentals of a service used by clients to observe ingested cloud events
/// </summary>
public interface ICloudEventHubClient
{

    /// <summary>
    /// Notifies clients about the successful ingestion of any and all <see cref="CloudEvent"/>
    /// </summary>
    /// <param name="e">The <see cref="CloudEvent"/> that has been ingested</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    Task StreamEvent(CloudEvent e, CancellationToken cancellationToken = default);

}