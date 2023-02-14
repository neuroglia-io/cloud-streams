using CloudStreams.Infrastructure.Configuration;

namespace CloudStreams.Infrastructure.Services;

/// <summary>
/// Represents the default implementation of the <see cref="IResourceWatcher{TResource}"/> interface
/// </summary>
/// <typeparam name="TResource">The type of <see cref="IResource"/> to watch</typeparam>
public class ResourceWatcher<TResource>
    : IResourceWatcher<TResource>
    where TResource : class, IResource, new()
{

    private bool _Disposed;

    /// <summary>
    /// Initializes a new <see cref="ResourceWatcher{TResource}"/>
    /// </summary>
    /// <param name="options">The options used to configure the <see cref="ResourceWatcher{TResource}"/></param>
    /// <param name="resources">The service used to manage resources</param>
    public ResourceWatcher(ResourceWatcherOptions options, IResourceRepository resources)
    {
        this.Options = options;
        this.Resources = resources;
    }

    /// <summary>
    /// Gets the current <see cref="ResourceWatcherOptions"/>
    /// </summary>
    protected ResourceWatcherOptions Options { get; }

    /// <summary>
    /// Gets the service used to manage resources
    /// </summary>
    protected IResourceRepository Resources { get; }


    /// <summary>
    /// Gets the <see cref="IObservable{T}"/> used to monitor the resource's state
    /// </summary>
    protected IObservable<IResourceWatchEvent<TResource>>? Observable { get; private set; }

    /// <inheritdoc/>
    public bool Running { get; private set; }

    /// <inheritdoc/>
    public virtual async ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        if (this.Running) return;
        this.Observable = await this.Resources.WatchResourcesAsync<TResource>(this.Options.Namespace, cancellationToken);
        this.Running = true;
    }

    /// <inheritdoc/>
    public virtual ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        if (!this.Running) return ValueTask.CompletedTask;
        this.Observable = null;
        this.Running = false;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public virtual IDisposable Subscribe(IObserver<IResourceWatchEvent<TResource>> observer)
    {
        if (this.Observable == null) throw new Exception("The resource watcher is not running");
        return this.Observable.Subscribe(observer);
    }

    /// <summary>
    /// Disposes of the <see cref="ResourceWatcher{TResource}"/>
    /// </summary>
    /// <param name="disposing">A boolean indicating whether or not the <see cref="ResourceWatcher{TResource}"/> is being disposed of</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!this._Disposed)
        {
            if (disposing)
            {
                this.Observable = null;
                this.Running = false;
            }
            this._Disposed = true;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

}
