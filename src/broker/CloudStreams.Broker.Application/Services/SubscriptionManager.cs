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

using CloudStreams.Broker.Application.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Neuroglia.Data.Infrastructure.ResourceOriented.Configuration;
using System.Collections.Concurrent;

namespace CloudStreams.Broker.Application.Services;

/// <summary>
/// Represents a service used to manage <see cref="Subscription"/>s
/// </summary>
/// <remarks>
/// Initializes a new <see cref="SubscriptionManager"/>
/// </remarks>
/// <param name="serviceProvider">The current <see cref="IServiceProvider"/></param>
/// <param name="loggerFactory">The service used to create <see cref="ILogger"/>s</param>
/// <param name="controllerOptions">The service used to access the current <see cref="IOptions{TOptions}"/></param>
/// <param name="repository">The service used to manage <see cref="IResource"/>s</param>
/// <param name="brokerOptions">The service used to access the current <see cref="Configuration.BrokerOptions"/></param>
public class SubscriptionManager(IServiceProvider serviceProvider, ILoggerFactory loggerFactory, IOptions<ResourceControllerOptions<Subscription>> controllerOptions, IResourceRepository repository, IOptions<BrokerOptions> brokerOptions)
    : ResourceController<Subscription>(loggerFactory, controllerOptions, repository)
{

    readonly ConcurrentDictionary<string, byte> _lockedKeys = new();

    /// <summary>
    /// Gets the current <see cref="IServiceProvider"/>
    /// </summary>
    protected IServiceProvider ServiceProvider { get; } = serviceProvider;

    /// <summary>
    /// Gets the running <see cref="Core.Resources.Broker"/>'s options
    /// </summary>
    protected BrokerOptions BrokerOptions { get; } = brokerOptions.Value;

    /// <summary>
    /// Gets a <see cref="ConcurrentDictionary{TKey, TValue}"/> containing the key/value mappings of handled <see cref="Subscription"/>s
    /// </summary>
    protected ConcurrentDictionary<string, SubscriptionHandler> Subscriptions { get; } = new();

    /// <summary>
    /// Gets the service used to monitor the current <see cref="Core.Resources.Broker"/>
    /// </summary>
    protected IResourceMonitor<Core.Resources.Broker>? Broker { get; private set; }

    /// <summary>
    /// Gets the <see cref="SubscriptionManager"/>'s <see cref="System.Threading.CancellationToken"/>
    /// </summary>
    protected CancellationToken CancellationToken => this.CancellationTokenSource.Token;

    /// <inheritdoc/>
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        Core.Resources.Broker? broker = null;
        using var activity = CloudStreamsDefaults.Telemetry.ActivitySource.StartActivity("SubscriptionManager.Start");
        try
        {
            broker = await this.Repository.GetAsync<Core.Resources.Broker>(this.BrokerOptions.Name, this.BrokerOptions.Namespace, cancellationToken).ConfigureAwait(false);
        }
        catch (ProblemDetailsException ex) when (ex.Problem.Status == (int)HttpStatusCode.NotFound) { }
        finally
        {
            if (broker == null)
            {
                broker = new Core.Resources.Broker(new ResourceMetadata(this.BrokerOptions.Name, this.BrokerOptions.Namespace), new BrokerSpec()
                {
                    Dispatch = new()
                    {
                        Sequencing = CloudEventSequencingConfiguration.Default
                    }
                });
                broker = await this.Repository.AddAsync(broker, false, cancellationToken).ConfigureAwait(false);
            }
            this.Broker = await this.Repository.MonitorAsync<Core.Resources.Broker>(this.BrokerOptions.Name, this.BrokerOptions.Namespace, false, cancellationToken).ConfigureAwait(false);
        }
        await base.StartAsync(cancellationToken).ConfigureAwait(false);
        foreach (var subscription in this.Resources.Values.ToList())
        {
            await this.OnSubscriptionCreatedAsync(subscription).ConfigureAwait(false);
        }
        this.Where(e => e.Type == ResourceWatchEventType.Created).Select(e => e.Resource).SubscribeAsync(this.OnSubscriptionCreatedAsync, cancellationToken);
        // every update is evaluated: label changes have to be detected per subscription, and OnSubscriptionLabelChangedAsync returns early when nothing changes
        this.Where(e => e.Type == ResourceWatchEventType.Updated).Select(s => s.Resource).SubscribeAsync(this.OnSubscriptionLabelChangedAsync, cancellationToken);
        this.Where(e => e.Type == ResourceWatchEventType.Deleted).Select(e => e.Resource).SubscribeAsync(this.OnSubscriptionDeletedAsync, cancellationToken);
        this.Broker!.Select(b => b.Resource.Spec.Selector).SubscribeAsync(this.OnBrokerSelectorChangedAsync, cancellationToken: cancellationToken);
        await this.OnBrokerSelectorChangedAsync(this.Broker!.Resource.Spec.Selector).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override async Task ReconcileAsync(CancellationToken cancellationToken = default)
    {
        using var activity = CloudStreamsDefaults.Telemetry.ActivitySource.StartActivity("SubscriptionManager.Reconcile");
        this.Options.LabelSelectors = this.Broker?.Resource.Spec.Selector?.Select(s => new LabelSelector(s.Key, LabelSelectionOperator.Equals, s.Value)).ToList();
        await base.ReconcileAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Builds a new cache key for the specified resource
    /// </summary>
    /// <param name="name">The name of the resource to build a new cache key for</param>
    /// <param name="namespace">The namespace the resource to build a new cache key for belongs to</param>
    /// <returns>A new cache key</returns>
    protected virtual string GetSubscriptionHandlerCacheKey(string name, string? @namespace) => string.IsNullOrWhiteSpace(@namespace) ? name : $"{@namespace}.{name}";

    /// <summary>
    /// Handles changes to the current broker's subscription selector
    /// </summary>
    /// <param name="selector">A key/value mapping of the labels the subscriptions to select must define</param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    protected virtual async Task OnBrokerSelectorChangedAsync(IDictionary<string, string>? selector)
    {
        using var activity = CloudStreamsDefaults.Telemetry.ActivitySource.StartActivity("SubscriptionManager.OnBrokerSelectorChanged");
        await this.ReconcileAsync(this.CancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Handles changes to the specified subscription's labels
    /// </summary>
    /// <param name="subscription">The subscription to manage the changes of</param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    protected virtual async Task OnSubscriptionLabelChangedAsync(Subscription subscription)
    {
        using var activity = CloudStreamsDefaults.Telemetry.ActivitySource.StartActivity("SubscriptionManager.OnSubscriptionLabelChanged");
        activity?.SetTag("subscription.name", subscription.GetName());
        activity?.SetTag("subscription.namespace", subscription.GetNamespace());
        if (this.Broker == null) return;
        var key = this.GetSubscriptionHandlerCacheKey(subscription.GetName(), subscription.GetNamespace());
        // a change must not be ignored because the subscription's handler is being created: wait for it, then evaluate the change
        while (this._lockedKeys.ContainsKey(key)) await Task.Delay(50, this.CancellationToken).ConfigureAwait(false);
        if (this.Options.LabelSelectors == null || this.Options.LabelSelectors.All(s => s.Selects(subscription)) == true)
        {
            if (this.Subscriptions.TryGetValue(key, out _)) return;
            await this.OnSubscriptionCreatedAsync(subscription).ConfigureAwait(false);
        }
        else
        {
            if (!this.Subscriptions.TryGetValue(key, out _)) return;
            await this.OnResourceDeletedAsync(subscription).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    protected override async Task OnResourceDeletedAsync(Subscription subscription, CancellationToken cancellationToken = default)
    {
        await base.OnResourceDeletedAsync(subscription, cancellationToken).ConfigureAwait(false);
        var key = this.GetSubscriptionHandlerCacheKey(subscription.GetName(), subscription.GetNamespace());
        if (!this.Subscriptions.TryRemove(key, out var handler) || handler == null) return;
        await handler.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Handles the creation of a new <see cref="Subscription"/>
    /// </summary>
    /// <param name="subscription">The newly created <see cref="Subscription"/></param>
    protected virtual async Task OnSubscriptionCreatedAsync(Subscription subscription)
    {
        using var activity = CloudStreamsDefaults.Telemetry.ActivitySource.StartActivity("SubscriptionManager.OnSubscriptionCreated");
        activity?.SetTag("subscription.name", subscription.GetName());
        activity?.SetTag("subscription.namespace", subscription.GetNamespace());
        var key = this.GetSubscriptionHandlerCacheKey(subscription.GetName(), subscription.GetNamespace());
        while (true)
        {
            if (this.Subscriptions.TryGetValue(key, out _)) return;
            if (this._lockedKeys.TryAdd(key, 0)) break;
            await Task.Delay(50, this.CancellationToken).ConfigureAwait(false);
        }
        SubscriptionHandler? handler = null;
        var initialized = false;
        try
        {
            handler = ActivatorUtilities.CreateInstance<SubscriptionHandler>(this.ServiceProvider, subscription, this.Broker!);
            await handler.InitializeAsync(this.CancellationToken).ConfigureAwait(false);
            this.Subscriptions.AddOrUpdate(key, handler, (_, _) => handler);
            initialized = true;
        }
        catch (Exception ex) when (!this.CancellationToken.IsCancellationRequested)
        {
            this.Logger.LogError(ex, "Failed to initialize the handler of subscription '{subscription}'. Retrying in {delay}...", subscription.GetQualifiedName(), HandlerInitializationRetryDelay);
        }
        catch (OperationCanceledException) { }
        finally
        {
            this._lockedKeys.TryRemove(key, out _);
            if (!initialized && handler != null) await handler.DisposeAsync().ConfigureAwait(false);
        }
        if (!initialized && !this.CancellationToken.IsCancellationRequested) _ = this.RetrySubscriptionCreationAsync(subscription);
    }

    /// <summary>
    /// Gets the delay after which the initialization of a subscription's handler is retried, when it has failed
    /// </summary>
    protected static readonly TimeSpan HandlerInitializationRetryDelay = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Retries to create the handler of the specified subscription, unless it has been deleted, deselected or handled in the meantime
    /// </summary>
    /// <param name="subscription">The subscription to retry creating the handler of</param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    protected virtual async Task RetrySubscriptionCreationAsync(Subscription subscription)
    {
        try
        {
            await Task.Delay(HandlerInitializationRetryDelay, this.CancellationToken).ConfigureAwait(false);
            var key = this.GetSubscriptionHandlerCacheKey(subscription.GetName(), subscription.GetNamespace());
            // the controller's cache only holds the subscriptions the broker selects, so a deleted or deselected subscription is not retried
            var current = this.Resources.Values.FirstOrDefault(s => s.GetName() == subscription.GetName() && s.GetNamespace() == subscription.GetNamespace());
            if (current == null || this.Subscriptions.ContainsKey(key)) return;
            await this.OnSubscriptionCreatedAsync(current).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { this.Logger.LogError(ex, "Failed to retry initializing the handler of subscription '{subscription}'", subscription.GetQualifiedName()); }
    }

    /// <summary>
    /// Handles the deletion of a new <see cref="Subscription"/>
    /// </summary>
    /// <param name="subscription">The newly deleted <see cref="Subscription"/></param>
    protected virtual async Task OnSubscriptionDeletedAsync(Subscription subscription)
    {
        using var activity = CloudStreamsDefaults.Telemetry.ActivitySource.StartActivity("SubscriptionManager.OnSubscriptionDeleted");
        activity?.SetTag("subscription.name", subscription.GetName());
        activity?.SetTag("subscription.namespace", subscription.GetNamespace());
        var key = this.GetSubscriptionHandlerCacheKey(subscription.GetName(), subscription.GetNamespace());
        if (this.Subscriptions.Remove(key, out var handler) && handler != null) await handler.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Disposes of the <see cref="SubscriptionHandler"/>
    /// </summary>
    /// <param name="disposing">A boolean indicating whether or not the <see cref="SubscriptionHandler"/> is being disposed of</param>
    protected override async ValueTask DisposeAsync(bool disposing)
    {
        if (!disposing) return;
        // handlers are disposed of first, while the manager's token source, to which they are linked, is still usable
        foreach (var handler in this.Subscriptions.Values.ToList()) await handler.DisposeAsync().ConfigureAwait(false);
        this.Subscriptions.Clear();
        if (this.Broker != null) await this.Broker.DisposeAsync().ConfigureAwait(false);
        await base.DisposeAsync(disposing);
        this.CancellationTokenSource?.Dispose();
    }

}
