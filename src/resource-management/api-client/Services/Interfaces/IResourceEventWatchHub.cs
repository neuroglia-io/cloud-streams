namespace CloudStreams.ResourceManagement.Api.Client.Services;

/// <summary>
/// Defines the fundamentals of a service used by clients to watch resource-related events
/// </summary>
public interface IResourceEventWatchHub
{

    /// <summary>
    /// Subscribes to events produced by resources of the specified type
    /// </summary>
    /// <param name="type">The type of resources to watch</param>
    /// <param name="namespace">The namespace the resources to watch belong to, if any</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    Task Subscribe(string type, string? @namespace = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Unsubscribes from events produced by resources of the specified type
    /// </summary>
    /// <param name="type">The type of resources to watch</param>
    /// <param name="namespace">The namespace the resources to watch belong to, if any</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    Task Unsubscribe(string type, string? @namespace = null, CancellationToken cancellationToken = default);

}
