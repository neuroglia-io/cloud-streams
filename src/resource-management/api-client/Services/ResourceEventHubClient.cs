using CloudStreams.Core.Data.Models;
using Microsoft.AspNetCore.SignalR.Client;
using System.Reactive.Subjects;

namespace CloudStreams.ResourceManagement.Api.Client.Services;

/// <summary>
/// Represents the service used to listen to <see cref="CloudEvent"/>s in real-time
/// </summary>
public class ResourceEventHubClient
    : IAsyncDisposable
{

    private bool _Disposed;

    /// <summary>
    /// Initializes a new <see cref="ResourceEventHubClient"/>
    /// </summary>
    /// <param name="connection">The underlying <see cref="HubConnection"/></param>
    public ResourceEventHubClient(HubConnection connection)
    {
        this.Connection = connection;
        this.Connection.On<CloudEvent>(nameof(IResourceEventWatchHubClient.ResourceWatchEvent), this.Subject.OnNext);
    }

    /// <summary>
    /// Gets the underlying <see cref="HubConnection"/>
    /// </summary>
    protected HubConnection Connection { get; }

    /// <summary>
    /// Gets the <see cref="Subject{T}"/> used to monitor ingested <see cref="CloudEvent"/>s
    /// </summary>
    protected Subject<CloudEvent> Subject { get; } = new();

    /// <summary>
    /// Starts the <see cref="ResourceEventHubClient"/> if it's not already running
    /// </summary>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    public virtual Task StartAsync() => this.Connection.State == HubConnectionState.Disconnected ? this.Connection.StartAsync() : Task.CompletedTask;

    /// <inheritdoc/>
    public IObservable<CloudEvent> SelectAll() => this.Subject;

    /// <summary>
    /// Disposes of the <see cref="ResourceEventHubClient"/>
    /// </summary>
    /// <param name="disposing">A boolean indicating whether or not the <see cref="ResourceEventHubClient"/> is being disposed of</param>
    protected virtual async ValueTask DisposeAsync(bool disposing)
    {
        if (this._Disposed) return;
        if (disposing)
        {
            this.Subject.Dispose();
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
