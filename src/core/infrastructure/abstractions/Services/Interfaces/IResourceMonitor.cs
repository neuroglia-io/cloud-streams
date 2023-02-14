namespace CloudStreams.Infrastructure.Services;

/// <summary>
/// Defines the fundamentals of a service used to monitor the state of a specific resource
/// </summary>
/// <typeparam name="TResource">The type of resource to monitor the state of</typeparam>
public interface IResourceMonitor<TResource>
    : IObservable<TResource>, IDisposable
{

    /// <summary>
    /// Gets the current state of the resource
    /// </summary>
    TResource State { get; }

    /// <summary>
    /// Gets a boolean indicating whether or not the <see cref="IResourceMonitor{TResource}"/> is monitoring the resource's state
    /// </summary>
    bool Running { get; }

    /// <summary>
    /// Starts monitoring the resource
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new awaitable <see cref="ValueTask"/></returns>
    ValueTask StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops monitoring the resource
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new awaitable <see cref="ValueTask"/></returns>
    ValueTask StopAsync(CancellationToken cancellationToken = default);

}