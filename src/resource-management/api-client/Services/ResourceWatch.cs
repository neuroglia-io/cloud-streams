using CloudStreams.Core.Data.Models;

namespace CloudStreams.ResourceManagement.Api.Client.Services;

/// <summary>
/// Represents the service used to handle a resource watch event subscription
/// </summary>
/// <typeparam name="TResource">The type of resources to watch</typeparam>
public class ResourceWatch<TResource>
    : IObservable<IResourceWatchEvent<TResource>>, IAsyncDisposable
    where TResource : class, IResource, new()
{

    private bool _Disposed;

    /// <summary>
    /// Initializes a new <see cref="ResourceWatch{TResource}"/>
    /// </summary>
    /// <param name="resourceWatchEventHub">The service used to interact with the <see cref="IResourceEventWatchHub"/></param>
    /// <param name="resourceNamespace">The namespace watched resources belong to, if any</param>
    /// <param name="stream">The <see cref="IObservable{T}"/> used to watch resources of the specified type</param>
    public ResourceWatch(ResourceWatchEventHubClient resourceWatchEventHub, string? resourceNamespace, IObservable<IResourceWatchEvent<TResource>> stream)
    {
        this.ResourceWatchEventHub = resourceWatchEventHub ?? throw new ArgumentNullException(nameof(resourceWatchEventHub));
        this.ResourceNamespace = resourceNamespace;
        this.Stream = stream;
    }

    /// <summary>
    /// Gets the service used to interact with the <see cref="IResourceEventWatchHub"/>
    /// </summary>
    protected ResourceWatchEventHubClient ResourceWatchEventHub { get; }

    /// <summary>
    /// Gets the namespace watched resources belong to, if any
    /// </summary>
    protected virtual string? ResourceNamespace { get; }

    /// <summary>
    /// Gets the <see cref="IObservable{T}"/> used to watch resources of the specified type
    /// </summary>
    protected IObservable<IResourceWatchEvent<TResource>> Stream { get; }

    /// <inheritdoc/>
    public IDisposable Subscribe(IObserver<IResourceWatchEvent<TResource>> observer) => this.Stream.Subscribe(observer);

    /// <summary>
    /// Disposes of the <see cref="ResourceWatch{TResource}"/>
    /// </summary>
    /// <param name="disposing">A boolean indicating whether or not the <see cref="ResourceWatch{TResource}"/> is being disposed of</param>
    protected virtual async ValueTask DisposeAsync(bool disposing)
    {
        if (!this._Disposed)
        {
            if (disposing)
            {
                await this.ResourceWatchEventHub.StopWatchingAsync<TResource>();
            }
            this._Disposed = true;
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await this.DisposeAsync(disposing: true).ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

}