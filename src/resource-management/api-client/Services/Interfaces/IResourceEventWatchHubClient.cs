using CloudStreams.Core.Data.Models;

namespace CloudStreams.ResourceManagement.Api.Client.Services;

/// <summary>
/// Defines the fundamentals of a service used by clients to watch resource-related events
/// </summary>
public interface IResourceEventWatchHubClient
{

    /// <summary>
    /// Notifies clients about a resource-related event
    /// </summary>
    /// <param name="e">The <see cref="Core.Data.Models.ResourceWatchEvent"/> that has been produced</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    Task ResourceWatchEvent(ResourceWatchEvent e, CancellationToken cancellationToken = default);

}
