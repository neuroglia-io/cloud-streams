﻿namespace CloudStreams.Core.Api.Client.Services;

/// <summary>
/// Represents the service used to handle a resource watch event subscription
/// </summary>
/// <typeparam name="TResource">The type of resources to watch</typeparam>
/// <remarks>
/// Initializes a new <see cref="ResourceWatch{TResource}"/>
/// </remarks>
/// <param name="resourceWatchEventHub">The service used to interact with the <see cref="IResourceEventWatchHub"/></param>
/// <param name="resourceNamespace">The namespace watched resources belong to, if any</param>
/// <param name="stream">The <see cref="IObservable{T}"/> used to watch resources of the specified type</param>
public class ResourceWatch<TResource>(ResourceWatchEventHubClient resourceWatchEventHub, string? resourceNamespace, IObservable<IResourceWatchEvent<TResource>> stream)
    : IObservable<IResourceWatchEvent<TResource>>, IAsyncDisposable
    where TResource : class, IResource, new()
{

    private bool _Disposed;

    /// <summary>
    /// Gets the service used to interact with the <see cref="IResourceEventWatchHub"/>
    /// </summary>
    protected ResourceWatchEventHubClient ResourceWatchEventHub { get; } = resourceWatchEventHub ?? throw new ArgumentNullException(nameof(resourceWatchEventHub));

    /// <summary>
    /// Gets the namespace watched resources belong to, if any
    /// </summary>
    protected virtual string? ResourceNamespace { get; } = resourceNamespace;

    /// <summary>
    /// Gets the <see cref="IObservable{T}"/> used to watch resources of the specified type
    /// </summary>
    protected IObservable<IResourceWatchEvent<TResource>> Stream { get; } = stream;

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