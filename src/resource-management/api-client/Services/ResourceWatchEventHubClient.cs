using CloudStreams.Core.Data.Models;
using Microsoft.AspNetCore.SignalR.Client;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace CloudStreams.ResourceManagement.Api.Client.Services;

/// <summary>
/// Represents the service used to listen to <see cref="CloudEvent"/>s in real-time
/// </summary>
public class ResourceWatchEventHubClient
    : IAsyncDisposable
{

    private bool _Disposed;

    /// <summary>
    /// Initializes a new <see cref="ResourceWatchEventHubClient"/>
    /// </summary>
    /// <param name="connection">The underlying <see cref="HubConnection"/></param>
    public ResourceWatchEventHubClient(HubConnection connection)
    {
        this.Connection = connection;
        this.Connection.On<ResourceWatchEvent>(nameof(IResourceEventWatchHubClient.ResourceWatchEvent), this.WatchEventStream.OnNext);
    }

    /// <summary>
    /// Gets the underlying <see cref="HubConnection"/>
    /// </summary>
    protected HubConnection Connection { get; }

    /// <summary>
    /// Gets the <see cref="Subject{T}"/> used to notify subscribers about incoming <see cref="IResourceWatchEvent"/>s
    /// </summary>
    protected Subject<ResourceWatchEvent> WatchEventStream { get; } = new();

    /// <summary>
    /// Starts the <see cref="ResourceWatchEventHubClient"/> if it's not already running
    /// </summary>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    public virtual Task StartAsync() => this.Connection.State == HubConnectionState.Disconnected ? this.Connection.StartAsync() : Task.CompletedTask;

    /// <summary>
    /// Watches resource of the specified type
    /// </summary>
    /// <typeparam name="TResource">The type of resources to watch</typeparam>
    /// <param name="namespace">The namespace resources to watch belong to</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IObservable{T}"/> used to observe incoming <see cref="IResourceWatchEvent"/>s for resources of the specified type</returns>
    public virtual async Task<ResourceWatch<TResource>> WatchAsync<TResource>(string? @namespace = null, CancellationToken cancellationToken = default)
        where TResource : class, IResource, new()
    {
        var resource = new TResource();
        await this.Connection.InvokeAsync(nameof(IResourceEventWatchHub.Watch), resource.Type, @namespace, cancellationToken).ConfigureAwait(false);
        var stream = this.WatchEventStream
            .Where(e => e.Resource.IsOfType<TResource>())
            .Select(e => e.ToType<TResource>());
        if(!string.IsNullOrWhiteSpace(@namespace)) stream = stream.Where(e => e.Resource.GetNamespace() == @namespace);
        return new(this, @namespace, stream);
    }

    /// <summary>
    /// Stops watching resources of the specified type
    /// </summary>
    /// <typeparam name="TResource">The type of resources to stop watching</typeparam>
    /// <param name="namespace">The namespace resources to stop watching belong to</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    public virtual async Task StopWatchingAsync<TResource>(string? @namespace = null, CancellationToken cancellationToken = default)
        where TResource : class, IResource, new()
    {
        var resource = new TResource();
        await this.Connection.InvokeAsync(nameof(IResourceEventWatchHub.StopWatching), resource.Type, @namespace, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Disposes of the <see cref="ResourceWatchEventHubClient"/>
    /// </summary>
    /// <param name="disposing">A boolean indicating whether or not the <see cref="ResourceWatchEventHubClient"/> is being disposed of</param>
    protected virtual async ValueTask DisposeAsync(bool disposing)
    {
        if (this._Disposed) return;
        if (disposing)
        {
            this.WatchEventStream.Dispose();
            await this.Connection.DisposeAsync();
        }
        this._Disposed = true;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await this.DisposeAsync(disposing: true);
        GC.SuppressFinalize(this);
    }

}
