// Copyright © 2023-Present The Cloud Streams Authors
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

using CloudStreams.Broker.Application.Configuration;
using Hylo;
using Hylo.Infrastructure.Services;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Net;
using System.Reactive.Linq;

namespace CloudStreams.Broker.Application.Services;

/// <summary>
/// Represents a service used to manage <see cref="Subscription"/>s
/// </summary>
public class SubscriptionManager
    : BackgroundService, IAsyncDisposable
{

    private bool _Disposed;

    /// <summary>
    /// Initializes a new <see cref="SubscriptionManager"/>
    /// </summary>
    /// <param name="serviceProvider">The current <see cref="IServiceProvider"/></param>
    /// <param name="resources">The service used to manage resources</param>
    /// <param name="subscriptionController">The service used to control <see cref="Subscription"/> resources</param>
    /// <param name="options">The service used to access the current <see cref="Configuration.BrokerOptions"/></param>
    public SubscriptionManager(IServiceProvider serviceProvider, IRepository resources, IResourceController<Subscription> subscriptionController, IOptions<BrokerOptions> options)
    {
        this.ServiceProvider = serviceProvider;
        this.Resources = resources;
        this.SubscriptionController = subscriptionController;
        this.Options = options.Value;
    }

    /// <summary>
    /// Gets the current <see cref="IServiceProvider"/>
    /// </summary>
    protected IServiceProvider ServiceProvider { get; }

    /// <summary>
    /// Gets the service used to manage resources
    /// </summary>
    protected IRepository Resources { get; }

    /// <summary>
    /// Gets the service used to control <see cref="Subscription"/> resources
    /// </summary>
    protected IResourceController<Subscription> SubscriptionController { get; }

    /// <summary>
    /// Gets the running <see cref="Core.Data.Broker"/>'s options
    /// </summary>
    protected BrokerOptions Options { get; }

    /// <summary>
    /// Gets a <see cref="ConcurrentDictionary{TKey, TValue}"/> containing the key/value mappings of handled <see cref="Subscription"/>s
    /// </summary>
    protected ConcurrentDictionary<string, SubscriptionHandler> Subscriptions { get; } = new();

    /// <summary>
    /// Gets the <see cref="SubscriptionManager"/>'s <see cref="System.Threading.CancellationTokenSource"/>
    /// </summary>
    protected CancellationTokenSource CancellationTokenSource { get; private set; } = null!;

    /// <summary>
    /// Gets the service used to monitor the current <see cref="Core.Data.Broker"/>
    /// </summary>
    protected IResourceMonitor<Core.Data.Broker>? Configuration { get; private set; }

    /// <summary>
    /// Gets the <see cref="SubscriptionManager"/>'s <see cref="System.Threading.CancellationToken"/>
    /// </summary>
    protected CancellationToken CancellationToken => this.CancellationTokenSource.Token;

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        this.CancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        Core.Data.Broker? broker = null;
        try
        {
            broker = await this.Resources.GetAsync<Core.Data.Broker>(this.Options.Name, this.Options.Namespace, stoppingToken).ConfigureAwait(false);
        }
        catch (HyloException ex) when (ex.Problem.Status == (int)HttpStatusCode.NotFound) { }
        finally
        {
            if (broker == null) await this.Resources.AddAsync(new Core.Data.Broker(new ResourceMetadata(this.Options.Name, this.Options.Namespace), new BrokerSpec()), false, stoppingToken).ConfigureAwait(false);
            this.Configuration = await this.Resources.MonitorAsync<Core.Data.Broker>(this.Options.Name, this.Options.Namespace, false, this.CancellationToken).ConfigureAwait(false);
        }
        foreach (var subscription in this.SubscriptionController.Resources.ToList())
        {
            await this.OnSubscriptionCreatedAsync(subscription).ConfigureAwait(false);
        }
        this.SubscriptionController.Where(e => e.Type == ResourceWatchEventType.Created).Select(e => e.Resource).SubscribeAsync(this.OnSubscriptionCreatedAsync, stoppingToken);
        this.SubscriptionController.Where(e => e.Type == ResourceWatchEventType.Deleted).Select(e => e.Resource).SubscribeAsync(this.OnSubscriptionDeletedAsync, stoppingToken);
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
        var handler = ActivatorUtilities.CreateInstance<SubscriptionHandler>(this.ServiceProvider, subscription, this.Configuration!);
        await handler.InitializeAsync(this.CancellationToken).ConfigureAwait(false);
        this.Subscriptions.AddOrUpdate(key, handler, (_, _) => handler);
    }

    /// <summary>
    /// Handles the deletion of a new <see cref="Subscription"/>
    /// </summary>
    /// <param name="subscription">The newly deleted <see cref="Subscription"/></param>
    protected virtual async Task OnSubscriptionDeletedAsync(Subscription subscription)
    {
        var key = this.GetResourceCacheKey(subscription.GetName(), subscription.GetNamespace());
        if (this.Subscriptions.Remove(key, out var handler) && handler != null) await handler.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Disposes of the <see cref="SubscriptionHandler"/>
    /// </summary>
    /// <param name="disposing">A boolean indicating whether or not the <see cref="SubscriptionHandler"/> is being disposed of</param>
    protected virtual async ValueTask DisposeAsync(bool disposing)
    {
        if (!this._Disposed)
        {
            if (disposing)
            {
                this.CancellationTokenSource?.Dispose();
                await this.Subscriptions.ToAsyncEnumerable().ForEachAsync(async s => await s.Value.DisposeAsync().ConfigureAwait(false)).ConfigureAwait(false);
                this.Subscriptions.Clear();
                if (this.Configuration != null) await this.Configuration.DisposeAsync().ConfigureAwait(false);
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
