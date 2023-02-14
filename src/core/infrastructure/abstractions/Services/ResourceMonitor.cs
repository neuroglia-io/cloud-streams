namespace CloudStreams.Infrastructure.Services;

/// <summary>
/// Represents the default implementation of the <see cref="IResourceRepository"/> interface
/// </summary>
/// <typeparam name="TResource">The type of <see cref="IResource"/> to monitor the state of</typeparam>
public class ResourceMonitor<TResource>
    : IResourceMonitor<TResource>
    where TResource : class, IResource, new()
{

    private bool _Disposed;

    /// <summary>
    /// Initializes a new <see cref="ResourceMonitor{TResource}"/>
    /// </summary>
    /// <param name="resources">The service used to manage resources</param>
    /// <param name="state">The current state of the resourcecurrent state of the resource</param>
    public ResourceMonitor(IResourceRepository resources, TResource state)
    {
        this.Resources = resources;
        this.State = state;
    }

    /// <summary>
    /// Gets the service used to manage resources
    /// </summary>
    protected IResourceRepository Resources { get; private set; }

    /// <summary>
    /// Gets the current state of the resource
    /// </summary>
    public TResource State { get; protected set; }

    /// <summary>
    /// Gets the <see cref="IObservable{T}"/> used to monitor the resource's state
    /// </summary>
    protected IObservable<TResource>? Observable { get; private set; }

    /// <summary>
    /// Gets an <see cref="IDisposable"/> that represents the <see cref="ResourceMonitor{TResource}"/>'s subscription to the monitored resource's state
    /// </summary>
    protected IDisposable? Subscription { get; private set; }

    /// <inheritdoc/>
    public bool Running { get; private set; }

    /// <inheritdoc/>
    public virtual async ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        if (this.Running) return;
        this.Observable = (await this.Resources.WatchResourcesAsync<TResource>(this.State.Metadata.Namespace, cancellationToken))
            .Where(e => (e.Type == ResourceWatchEventType.Updated || e.Type == ResourceWatchEventType.Deleted) && e.State == this.State)
            .TakeUntil(e => e.Type == ResourceWatchEventType.Deleted)
            .Select(e => e.State);
        this.Subscription = this.Observable.Subscribe(this.OnStateChanged);
        this.Running = true;
    }

    /// <inheritdoc/>
    public virtual ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        if (!this.Running) return ValueTask.CompletedTask;
        this.Observable = null;
        this.Subscription?.Dispose();
        this.Subscription = null;
        this.Running = false;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public virtual IDisposable Subscribe(IObserver<TResource> observer)
    {
        if (this.Observable == null) throw new Exception("The resource monitor is not running");
        return this.Observable.Subscribe(observer);
    }

    /// <summary>
    /// Handles changes to the monitored resource's state
    /// </summary>
    /// <param name="state">The resource's updated state</param>
    protected virtual void OnStateChanged(TResource state)
    {
        this.State = state;
    }

    /// <summary>
    /// Disposes of the <see cref="ResourceMonitor{TResource}"/>
    /// </summary>
    /// <param name="disposing">A boolean indicating whether or not the <see cref="ResourceMonitor{TResource}"/> is being disposed</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!this._Disposed)
        {
            if (disposing)
            {
                this.Observable = null;
                this.Subscription?.Dispose();
                this.Subscription = null;
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
