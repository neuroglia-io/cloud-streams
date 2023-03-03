using System.Reactive.Subjects;

namespace CloudStreams.Core.Infrastructure.Services;

/// <summary>
/// Represents the default implementation of the <see cref="IResourceWatcher"/> interface
/// </summary>
internal class K8sResourceWatcher
    : IResourceWatcher
{

    private bool _Disposed;

    /// <summary>
    /// Initializes a new <see cref="K8sResourceWatcher"/>
    /// </summary>
    /// <param name="subject">The <see cref="Subject{T}"/> used to notify about consumed <see cref="IResourceWatchEvent"/>s</param>
    /// <param name="subscription">An <see cref="IDisposable"/> that represents the subscription, if any</param>
    public K8sResourceWatcher(Subject<IResourceWatchEvent> subject, IDisposable subscription)
    {
        this.Subject = subject ?? throw new NullReferenceException(nameof(subject));
        this.Subscription = subscription ?? throw new NullReferenceException(nameof(subscription));
    }

    /// <summary>
    /// Gets the <see cref="Subject{T}"/> used to notify about consumed <see cref="IResourceWatchEvent"/>s
    /// </summary>
    protected Subject<IResourceWatchEvent> Subject { get; }

    /// <summary>
    /// Gets an <see cref="IDisposable"/> that represents the subscription, if any
    /// </summary>
    protected IDisposable Subscription { get; }

    /// <inheritdoc/>
    public virtual IDisposable Subscribe(IObserver<IResourceWatchEvent> observer) => this.Subject.Subscribe(observer);

    /// <summary>
    /// Disposes of the <see cref="K8sResourceWatcher{TResource}"/>
    /// </summary>
    /// <param name="disposing">A boolean indicating whether or not the <see cref="K8sResourceWatcher"/> is being disposed of</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!this._Disposed)
        {
            if (disposing)
            {
                this.Subject.Dispose();
                this.Subscription?.Dispose();
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

/// <summary>
/// Represents the default implementation of the <see cref="IResourceWatcher{TResource}"/> interface
/// </summary>
/// <typeparam name="TResource">The type of <see cref="IResource"/> to watch</typeparam>
internal class K8sResourceWatcher<TResource>
    : IResourceWatcher<TResource>
    where TResource : class, IResource, new()
{

    private bool _Disposed;

    /// <summary>
    /// Initializes a new <see cref="K8sResourceWatcher{TResource}"/>
    /// </summary>
    /// <param name="subject">The <see cref="Subject{T}"/> used to notify about consumed <see cref="IResourceWatchEvent"/>s</param>
    /// <param name="subscription">An <see cref="IDisposable"/> that represents the subscription, if any</param>
    public K8sResourceWatcher(Subject<IResourceWatchEvent<TResource>> subject, IDisposable subscription)
    {
        this.Subject = subject ?? throw new NullReferenceException(nameof(subject));
        this.Subscription = subscription ?? throw new NullReferenceException(nameof(subscription));
    }

    /// <summary>
    /// Gets the <see cref="Subject{T}"/> used to notify about consumed <see cref="IResourceWatchEvent"/>s
    /// </summary>
    protected Subject<IResourceWatchEvent<TResource>> Subject { get; }

    /// <summary>
    /// Gets an <see cref="IDisposable"/> that represents the subscription, if any
    /// </summary>
    protected IDisposable Subscription { get; }

    /// <inheritdoc/>
    public virtual IDisposable Subscribe(IObserver<IResourceWatchEvent<TResource>> observer) => this.Subject.Subscribe(observer);

    /// <summary>
    /// Disposes of the <see cref="K8sResourceWatcher{TResource}"/>
    /// </summary>
    /// <param name="disposing">A boolean indicating whether or not the <see cref="K8sResourceWatcher{TResource}"/> is being disposed of</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!this._Disposed)
        {
            if (disposing)
            {
                this.Subject.Dispose();
                this.Subscription?.Dispose();
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
