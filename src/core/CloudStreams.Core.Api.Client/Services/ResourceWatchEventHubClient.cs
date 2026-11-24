// Copyright © 2024-Present The Cloud Streams Authors
//
// Licensed under the Apache License, Version 2.0 (the "License"),
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using Microsoft.AspNetCore.SignalR.Client;
using Neuroglia.Eventing.CloudEvents;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace CloudStreams.Core.Api.Client.Services;

/// <summary>
/// Represents the service used to listen to <see cref="ResourceWatchEvent"/>s
/// </summary>
/// <remarks>
/// <para>
/// Watches are reference-counted per resource type and namespace, so that several consumers can watch the same resources, and the server is only told to stop
/// watching once the last of them stops.
/// </para>
/// <para>
/// The server forgets the watches of a connection when it drops. They are therefore registered again after every reconnection, and <see cref="Reconnected"/> lets
/// consumers fetch whatever changed in the meantime. A connection that closes for good is restarted with backoff (see #192)
/// </para>
/// </remarks>
public class ResourceWatchEventHubClient
    : IAsyncDisposable
{

    readonly Lock _lock = new();
    readonly Dictionary<string, WatchRegistration> _registrations = [];
    readonly CancellationTokenSource _cancellationTokenSource = new();
    bool _disposed;

    /// <summary>
    /// Initializes a new <see cref="ResourceWatchEventHubClient"/>
    /// </summary>
    /// <param name="connection">The underlying <see cref="HubConnection"/></param>
    public ResourceWatchEventHubClient(HubConnection connection)
    {
        this.Connection = connection;
        this.Connection.On<ResourceWatchEvent>(nameof(IResourceEventWatchHubClient.ResourceWatchEvent), this.WatchEventStream.OnNext);
        this.Connection.Reconnecting += _ =>
        {
            this.ConnectionStateSubject.OnNext(this.Connection.State);
            return Task.CompletedTask;
        };
        this.Connection.Reconnected += this.OnReconnectedAsync;
        this.Connection.Closed += this.OnClosedAsync;
    }

    /// <summary>
    /// Gets the underlying <see cref="HubConnection"/>
    /// </summary>
    protected HubConnection Connection { get; }

    /// <summary>
    /// Gets the <see cref="Subject{T}"/> used to stream <see cref="ResourceWatchEvent"/>s
    /// </summary>
    protected Subject<ResourceWatchEvent> WatchEventStream { get; } = new();

    /// <summary>
    /// Gets the <see cref="BehaviorSubject{T}"/> used to stream the state of the connection
    /// </summary>
    protected BehaviorSubject<HubConnectionState> ConnectionStateSubject { get; } = new(HubConnectionState.Disconnected);

    /// <summary>
    /// Gets the <see cref="Subject{T}"/> used to notify consumers that the connection has been re-established, and that watched resources may have changed in the meantime
    /// </summary>
    protected Subject<string?> ReconnectedSubject { get; } = new();

    /// <summary>
    /// Gets an <see cref="IObservable{T}"/> used to observe the state of the connection
    /// </summary>
    public IObservable<HubConnectionState> ConnectionState => this.ConnectionStateSubject.DistinctUntilChanged();

    /// <summary>
    /// Gets an <see cref="IObservable{T}"/> that produces a value every time the connection has been re-established, after which watched resources should be listed again
    /// </summary>
    public IObservable<string?> Reconnected => this.ReconnectedSubject;

    /// <summary>
    /// Starts the client, if it is not started yet
    /// </summary>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    public virtual async Task StartAsync()
    {
        if (this.Connection.State != HubConnectionState.Disconnected) return;
        this.ConnectionStateSubject.OnNext(HubConnectionState.Connecting);
        try
        {
            await this.Connection.StartAsync(this._cancellationTokenSource.Token).ConfigureAwait(false);
        }
        finally
        {
            this.ConnectionStateSubject.OnNext(this.Connection.State);
        }
    }

    /// <summary>
    /// Watches resources of the specified type
    /// </summary>
    /// <typeparam name="TResource">The type of resources to watch</typeparam>
    /// <param name="namespace">The namespace the resources to watch belong to, if any</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="ResourceWatch{TResource}"/></returns>
    public virtual async Task<ResourceWatch<TResource>> WatchAsync<TResource>(string? @namespace = null, CancellationToken cancellationToken = default)
        where TResource : class, IResource, new()
    {
        var definition = new TResource().Definition;
        var key = GetRegistrationKey(definition, @namespace);
        bool register;
        lock (this._lock)
        {
            if (this._registrations.TryGetValue(key, out var registration)) registration.Count++;
            else this._registrations[key] = new(definition, @namespace) { Count = 1 };
            register = this._registrations[key].Count == 1;
        }
        try
        {
            if (register) await this.Connection.InvokeAsync(nameof(IResourceEventWatchHub.Watch), definition, @namespace, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            this.Release(key);
            throw;
        }
        var stream = this.WatchEventStream
            .Where(e => e.Resource.IsOfType<TResource>())
            .Select(e => e.OfType<TResource>());
        if (!string.IsNullOrWhiteSpace(@namespace)) stream = stream.Where(e => e.Resource.GetNamespace() == @namespace);
        return new(this, @namespace, stream);
    }

    /// <summary>
    /// Stops watching resources of the specified type
    /// </summary>
    /// <typeparam name="TResource">The type of resources to stop watching</typeparam>
    /// <param name="namespace">The namespace the resources to stop watching belong to, if any</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    public virtual async Task StopWatchingAsync<TResource>(string? @namespace = null, CancellationToken cancellationToken = default)
        where TResource : class, IResource, new()
    {
        var definition = new TResource().Definition;
        if (!this.Release(GetRegistrationKey(definition, @namespace))) return;
        if (this.Connection.State != HubConnectionState.Connected) return;
        await this.Connection.InvokeAsync(nameof(IResourceEventWatchHub.StopWatching), definition, @namespace, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Releases a reference to the specified watch registration
    /// </summary>
    /// <param name="key">The key of the registration to release</param>
    /// <returns>A boolean indicating whether or not the last reference to the registration has been released</returns>
    protected virtual bool Release(string key)
    {
        lock (this._lock)
        {
            if (!this._registrations.TryGetValue(key, out var registration)) return false;
            registration.Count--;
            if (registration.Count > 0) return false;
            this._registrations.Remove(key);
            return true;
        }
    }

    /// <summary>
    /// Handles the re-establishment of the connection, by registering active watches again
    /// </summary>
    /// <param name="connectionId">The id of the new connection</param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    protected virtual async Task OnReconnectedAsync(string? connectionId)
    {
        List<WatchRegistration> registrations;
        lock (this._lock) registrations = [.. this._registrations.Values];
        foreach (var registration in registrations)
        {
            try { await this.Connection.InvokeAsync(nameof(IResourceEventWatchHub.Watch), registration.Definition, registration.Namespace, this._cancellationTokenSource.Token).ConfigureAwait(false); }
            catch (Exception ex) when (ex is not OperationCanceledException) { /* retried on the next reconnection */ }
        }
        this.ConnectionStateSubject.OnNext(this.Connection.State);
        this.ReconnectedSubject.OnNext(connectionId);
    }

    /// <summary>
    /// Handles the closing of the connection, after automatic reconnection gave up, by restarting it with backoff
    /// </summary>
    /// <param name="error">The error that closed the connection, if any</param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    protected virtual async Task OnClosedAsync(Exception? error)
    {
        this.ConnectionStateSubject.OnNext(HubConnectionState.Disconnected);
        var attempt = 0;
        while (!this._disposed && !this._cancellationTokenSource.IsCancellationRequested && this.Connection.State == HubConnectionState.Disconnected)
        {
            var delay = TimeSpan.FromSeconds(Math.Min(Math.Pow(2, attempt++), 30));
            try
            {
                await Task.Delay(delay, this._cancellationTokenSource.Token).ConfigureAwait(false);
                await this.StartAsync().ConfigureAwait(false);
                await this.OnReconnectedAsync(this.Connection.ConnectionId).ConfigureAwait(false);
                return;
            }
            catch (OperationCanceledException) { return; }
            catch { /* the connection could not be restarted: try again */ }
        }
    }

    static string GetRegistrationKey(ResourceDefinitionInfo definition, string? @namespace) => string.IsNullOrWhiteSpace(@namespace) ? definition.ToString() : $"{definition}/{@namespace}";

    /// <summary>
    /// Disposes of the <see cref="ResourceWatchEventHubClient"/>
    /// </summary>
    /// <param name="disposing">A boolean indicating whether or not the <see cref="ResourceWatchEventHubClient"/> is being disposed of</param>
    /// <returns>A new awaitable <see cref="ValueTask"/></returns>
    protected virtual async ValueTask DisposeAsync(bool disposing)
    {
        if (this._disposed) return;
        this._disposed = true;
        if (!disposing) return;
        this._cancellationTokenSource.Cancel();
        this.Connection.Reconnected -= this.OnReconnectedAsync;
        this.Connection.Closed -= this.OnClosedAsync;
        await this.Connection.DisposeAsync().ConfigureAwait(false);
        this.WatchEventStream.Dispose();
        this.ConnectionStateSubject.Dispose();
        this.ReconnectedSubject.Dispose();
        this._cancellationTokenSource.Dispose();
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await this.DisposeAsync(disposing: true).ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Describes a watch registered on the server
    /// </summary>
    /// <param name="Definition">The type of the watched resources</param>
    /// <param name="Namespace">The namespace of the watched resources, if any</param>
    protected sealed record WatchRegistration(ResourceDefinitionInfo Definition, string? Namespace)
    {

        /// <summary>
        /// Gets/sets the number of consumers of the watch
        /// </summary>
        public int Count { get; set; }

    }

}
