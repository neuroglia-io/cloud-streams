﻿using CloudStreams.Core.Infrastructure.Services;
using System.Collections.Concurrent;
using System.Reactive.Linq;

namespace CloudStreams.Broker.Application.Services;

/// <summary>
/// Represents a service used to manage <see cref="Subscription"/>s
/// </summary>
public class SubscriptionManager
    : BackgroundService
{

    private bool _Disposed;

    /// <summary>
    /// Initializes a new <see cref="SubscriptionManager"/>
    /// </summary>
    /// <param name="serviceProvider">The current <see cref="IServiceProvider"/></param>
    /// <param name="subscriptionController">The service used to control <see cref="Subscription"/> resources</param>
    public SubscriptionManager(IServiceProvider serviceProvider, IResourceController<Subscription> subscriptionController)
    {
        this.ServiceProvider = serviceProvider;
        this.SubscriptionController = subscriptionController;
    }

    /// <summary>
    /// Gets the current <see cref="IServiceProvider"/>
    /// </summary>
    protected IServiceProvider ServiceProvider { get; }

    /// <summary>
    /// Gets the service used to control <see cref="Subscription"/> resources
    /// </summary>
    protected IResourceController<Subscription> SubscriptionController { get; }

    /// <summary>
    /// Gets a <see cref="ConcurrentDictionary{TKey, TValue}"/> containing the key/value mappings of handled <see cref="Subscription"/>s
    /// </summary>
    protected ConcurrentDictionary<string, SubscriptionHandler> Subscriptions { get; } = new();

    /// <summary>
    /// Gets the <see cref="SubscriptionManager"/>'s <see cref="System.Threading.CancellationTokenSource"/>
    /// </summary>
    protected CancellationTokenSource CancellationTokenSource { get; private set; } = null!;

    /// <summary>
    /// Gets the <see cref="SubscriptionManager"/>'s <see cref="System.Threading.CancellationToken"/>
    /// </summary>
    protected CancellationToken CancellationToken => this.CancellationTokenSource.Token;

    /// <inheritdoc/>
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        this.CancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        this.SubscriptionController.Where(e => e.Type == ResourceWatchEventType.Created).Select(e => e.Resource).SubscribeAsync(this.OnSubscriptionCreatedAsync, stoppingToken);
        this.SubscriptionController.Where(e => e.Type == ResourceWatchEventType.Deleted).Select(e => e.Resource).SubscribeAsync(this.OnSubscriptionDeletedAsync, stoppingToken);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Builds a new cache key for the specified resource
    /// </summary>
    /// <param name="name">The name of the resource to build a new cache key for</param>
    /// <param name="namespace">The namespace the resource to build a new cache key for belongs to</param>
    /// <returns>A new cache key</returns>
    protected virtual string GetResourceCacheKey(string name, string? @namespace) => string.IsNullOrWhiteSpace(@namespace) ? name : $"{@namespace}.{name}";

    /// <summary>
    /// Handles the creation of a new <see cref="Subscription"/>
    /// </summary>
    /// <param name="subscription">The newly created <see cref="Subscription"/></param>
    protected virtual async Task OnSubscriptionCreatedAsync(Subscription subscription)
    {
        var key = this.GetResourceCacheKey(subscription.GetName(), subscription.GetNamespace());
        var handler = ActivatorUtilities.CreateInstance<SubscriptionHandler>(this.ServiceProvider, subscription);
        await handler.InitializeAsync(this.CancellationToken).ConfigureAwait(false);
        this.Subscriptions.AddOrUpdate(key, handler, (_, _) => handler);
    }

    /// <summary>
    /// Handles the deletion of a new <see cref="Subscription"/>
    /// </summary>
    /// <param name="subscription">The newly deleted <see cref="Subscription"/></param>
    protected virtual Task OnSubscriptionDeletedAsync(Subscription subscription)
    {
        var key = this.GetResourceCacheKey(subscription.GetName(), subscription.GetNamespace());
        if (this.Subscriptions.Remove(key, out var dispatcher) && dispatcher != null) dispatcher.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Disposes of the <see cref="SubscriptionHandler"/>
    /// </summary>
    /// <param name="disposing">A boolean indicating whether or not the <see cref="SubscriptionHandler"/> is being disposed of</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!this._Disposed)
        {
            if (disposing)
            {
                this.CancellationTokenSource.Dispose();
                this.Subscriptions.ToList().ForEach(s => s.Value.Dispose());
                this.Subscriptions.Clear();
            }
            this._Disposed = true;
        }
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        base.Dispose();
        GC.SuppressFinalize(this);
    }

}