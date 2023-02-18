namespace CloudStreams.Core.Infrastructure.Services;

/// <summary>
/// Defines the fundamentals of a service used to watch resources
/// </summary>
/// <typeparam name="TResource">The type of resource to watch</typeparam>
public interface IResourceWatcher<TResource>
    : IObservable<IResourceWatchEvent<TResource>>, IDisposable
    where TResource : class, IResource, new()
{

    /// <summary>
    /// Gets a boolean indicating whether or not the <see cref="IResourceMonitor{TResource}"/> is running
    /// </summary>
    bool Running { get; }

    /// <summary>
    /// Starts watching resources
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new awaitable <see cref="ValueTask"/></returns>
    ValueTask StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops watching resources
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new awaitable <see cref="ValueTask"/></returns>
    ValueTask StopAsync(CancellationToken cancellationToken = default);

}
