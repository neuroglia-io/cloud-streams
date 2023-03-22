using CloudStreams.Core;
using CloudStreams.Core.Data.Models;
using CloudStreams.Core.Infrastructure.Services;
using CloudStreams.ResourceManagement.Api.Client.Services;
using CloudStreams.ResourceManagement.Api.Hubs;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace CloudStreams.ResourceManagement.Api.Services;

/// <summary>
/// Represents a service used to dispatch <see cref="ResourceWatchEvent"/>s to all <see cref="IResourceEventWatchHubClient"/>s
/// </summary>
public class ResourceWatchEventHubController
    : BackgroundService
{

    /// <summary>
    /// Initializes a new <see cref="ResourceWatchEventHubController"/>
    /// </summary>
    /// <param name="resources">The service used to manage <see cref="IResource"/>s</param>
    /// <param name="hubContext">The current <see cref="IResourceEventWatchHubClient"/>'s <see cref="IHubContext{THub, T}"/></param>
    public ResourceWatchEventHubController(IResourceRepository resources, IHubContext<ResourceEventWatchHub, IResourceEventWatchHubClient> hubContext)
    {
        this.Resources = resources;
        this.HubContext = hubContext;
    }

    /// <summary>
    /// Gets the service used to manage <see cref="IResource"/>s
    /// </summary>
    protected IResourceRepository Resources { get; }

    /// <summary>
    /// Gets the current <see cref="IResourceEventWatchHubClient"/>'s <see cref="IHubContext{THub, T}"/>
    /// </summary>
    protected IHubContext<ResourceEventWatchHub, IResourceEventWatchHubClient> HubContext { get; }

    /// <summary>
    /// Gets a <see cref="ConcurrentDictionary{TKey, TValue}"/> containing the mapping of active ^subscriptions per hub connection id
    /// </summary>
    protected ConcurrentDictionary<string, ConcurrentDictionary<string, IResourceWatcher>> Connections { get; } = new();

    /// <summary>
    /// Gets the <see cref="ResourceWatchEventHubController"/>'s <see cref="System.Threading.CancellationTokenSource"/>
    /// </summary>
    protected CancellationTokenSource CancellationTokenSource { get; private set; } = null!;

    /// <inheritdoc/>
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        this.CancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Watches resources of the specified type
    /// </summary>
    /// <param name="connectionId">The id of the SignalR connection to create the watch for</param>
    /// <param name="type">The type of resource to watch</param>
    /// <param name="namespace">The namespace resources to watch belong to, if any</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    public virtual async Task WatchResourcesAsync(string connectionId, ResourceType type, string? @namespace = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(connectionId)) throw new ArgumentNullException(nameof(connectionId));
        if (type == null) throw new ArgumentNullException(nameof(type));
        var subscriptionKey = this.GetSubscriptionKey(type, @namespace);
        if (this.Connections.TryGetValue(connectionId, out var subscriptions) && subscriptions != null && subscriptions.TryGetValue(subscriptionKey, out var watcher) && watcher != null) return;
        if (subscriptions == null)
        {
            subscriptions = new();
            this.Connections.AddOrUpdate(connectionId, subscriptions, (key, current) =>
            {
                current.Values.ToList().ForEach(d => d.Dispose());
                return subscriptions;
            });
        }
        watcher = await this.Resources.WatchResourcesAsync(type.Group, type.Version, type.Plural, @namespace, cancellationToken: cancellationToken).ConfigureAwait(false);
        watcher.SubscribeAsync(e => this.OnResourceWatchEventAsync(connectionId, e));
        subscriptions.AddOrUpdate(subscriptionKey, watcher, (key, current) =>
        {
            current.Dispose();
            return watcher;
        });
    }

    /// <summary>
    /// Stop watching resources of the specified type
    /// </summary>
    /// <param name="connectionId">The id of the SignalR connection that owns the watch to dispose of</param>
    /// <param name="type">The type of resource to stop watching</param>
    /// <param name="namespace">The namespace resources to stop watching belong to, if any</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    public virtual Task StopWatchingResourcesAsync(string connectionId, ResourceType type, string? @namespace = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(connectionId)) throw new ArgumentNullException(nameof(connectionId));
        if (type == null) throw new ArgumentNullException(nameof(type));
        if (!this.Connections.TryGetValue(connectionId, out var subscriptions) || subscriptions == null || !subscriptions.Any()) return Task.CompletedTask;
        var subscriptionKey = this.GetSubscriptionKey(type, @namespace);
        if (subscriptions.Remove(subscriptionKey, out var subscription) && subscription != null) subscription.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Releases all resources owned by the specified connection
    /// </summary>
    /// <param name="connectionId">The id of the connection to release the resources of</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    public virtual Task ReleaseConnectionResourcesAsync(string connectionId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(connectionId)) throw new ArgumentNullException(nameof(connectionId));
        if (!this.Connections.Remove(connectionId, out var subscriptions) || subscriptions == null || !subscriptions.Any()) return Task.CompletedTask;
        subscriptions.Keys.ToList().ForEach(subscriptionId =>
        {
            subscriptions.Remove(subscriptionId, out var subscription);
            subscription?.Dispose();
        });
        return Task.CompletedTask;
    }

    /// <summary>
    /// Creates a new subscription key for the specified resource type and namespace
    /// </summary>
    /// <param name="type">The type of resources to create a new subscription key for</param>
    /// <param name="namespace">The namespace the resources to create a new subscription key for belong to</param>
    /// <returns>A new subscription key for the specified resource type and namespace</returns>
    protected virtual string GetSubscriptionKey(ResourceType type, string? @namespace = null) => string.IsNullOrWhiteSpace(@namespace) ? type.ToString() : $"{type}/{@namespace}";

    /// <summary>
    /// Handles the specified <see cref="IResourceWatchEvent"/>
    /// </summary>
    /// <param name="connectionId">The id of the connection the event has been produced for</param>
    /// <param name="e">The <see cref="IResourceWatchEvent"/> to handle</param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    protected virtual Task OnResourceWatchEventAsync(string connectionId, IResourceWatchEvent e) => this.HubContext.Clients.Client(connectionId).ResourceWatchEvent(e);

    /// <inheritdoc/>
    public override void Dispose()
    {
        this.CancellationTokenSource?.Dispose();
        this.Connections.Keys.ToList().ForEach(connectionId =>
        {
            this.Connections.Remove(connectionId, out var subscriptions);
            if (subscriptions == null) return;
            subscriptions.Keys.ToList().ForEach(subscriptionId =>
            {
                subscriptions.Remove(subscriptionId, out var subscription);
                subscription?.Dispose();
            });
        });
        base.Dispose();
        GC.SuppressFinalize(this);
    }

}