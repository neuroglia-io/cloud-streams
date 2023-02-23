using CloudStreams.Core.Data.Models;

namespace CloudStreams.ResourceManagement.Api.Client.Services;

/// <summary>
/// Defines the fundamentals of a service used by clients to observe ingested cloud events
/// </summary>
public interface IResourceEventHubClient
{

    /// <summary>
    /// Notifies clients about the succesfull ingestion of any and all <see cref="CloudEvent"/>
    /// </summary>
    /// <param name="e">The <see cref="CloudEvent"/> that has been ingested</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    Task StreamEvent(CloudEvent e, CancellationToken cancellationToken = default);

}
