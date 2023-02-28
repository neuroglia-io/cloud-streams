using CloudStreams.Channel.Application.Configuration;

namespace CloudStreams.Channel.Application.Services;

/// <summary>
/// Represents the service used to dispatch consumed <see cref="CloudEvent"/>s to controlled <see cref="Subscription"/>s
/// </summary>
public class ChannelCloudEventDispatcher
    : BackgroundService
{

    /// <summary>
    /// Initializes a new <see cref="ChannelCloudEventDispatcher"/>
    /// </summary>
    /// <param name="serviceProvider">The current <see cref="IServiceProvider"/></param>
    /// <param name="loggerFactory">The service used to create <see cref="ILogger"/>s</param>
    /// <param name="channelOptions">The service used to access the current <see cref="Configuration.ChannelOptions"/></param>
    /// <param name="resourceRepository">The service used to manage <see cref="IResource"/>s</param>
    /// <param name="cloudEventStream">The service used to stream inbound <see cref="CloudEvent"/>s</param>
    /// <param name="subscriptionController">The service used to control <see cref="Subscription"/> resources</param>
    public ChannelCloudEventDispatcher(IServiceProvider serviceProvider, ILoggerFactory loggerFactory, IOptions<ChannelOptions> channelOptions, 
        IResourceRepository resourceRepository, ICloudEventStream cloudEventStream, IResourceController<Subscription> subscriptionController)
    {
        this.ServiceProvider = serviceProvider;
        this.Logger = loggerFactory.CreateLogger(this.GetType());
        this.ChannelOptions = channelOptions.Value;
        this.ResourceRepository = resourceRepository;
        this.CloudEventStream = cloudEventStream;
        this.SubscriptionController = subscriptionController;
    }

    /// <summary>
    /// Gets the current <see cref="IServiceProvider"/>
    /// </summary>
    protected IServiceProvider ServiceProvider { get; }

    /// <summary>
    /// Gets the service used to perform logging
    /// </summary>
    protected ILogger Logger { get; }

    /// <summary>
    /// Gets the object used to configure the current Cloud Streams cloud event channel
    /// </summary>
    protected ChannelOptions ChannelOptions { get; }

    /// <summary>
    /// Gets the service used to manage <see cref="IResource"/>s
    /// </summary>
    protected IResourceRepository ResourceRepository { get; }

    /// <summary>
    /// Gets the service used to stream inbound <see cref="CloudEvent"/>s
    /// </summary>
    protected ICloudEventStream CloudEventStream { get; }

    /// <summary>
    /// Gets the service used to control <see cref="Subscription"/> resources
    /// </summary>
    protected IResourceController<Subscription> SubscriptionController { get; }

    /// <summary>
    /// Gets the service used to monitor the channel's configuration
    /// </summary>
    protected IResourceMonitor<Core.Data.Models.Channel> Channel { get; private set; } = null!;

    /// <summary>
    /// Gets a <see cref="ConcurrentDictionary{TKey, TValue}"/> used to cache active <see cref="Subscription"/>s
    /// </summary>
    protected ConcurrentDictionary<string, SubscriptionCloudEventDispatcher> Subscriptions { get; } = new();

    /// <summary>
    /// Gets the <see cref="ChannelCloudEventDispatcher"/>'s <see cref="System.Threading.CancellationTokenSource"/>
    /// </summary>
    protected CancellationTokenSource CancellationTokenSource { get; private set; } = null!;

    /// <summary>
    /// Gets the <see cref="ChannelCloudEventDispatcher"/>'s <see cref="System.Threading.CancellationToken"/>
    /// </summary>
    protected CancellationToken CancellationToken => this.CancellationTokenSource.Token;

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        this.CancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        this.Channel = await this.ResourceRepository.MonitorAsync<Core.Data.Models.Channel>(this.ChannelOptions.Name, this.ChannelOptions.Namespace, cancellationToken: this.CancellationToken);
        var desiredOffset = this.Channel.Resource.Spec.Stream?.Offset;
        var ackedOffset = this.Channel.Resource.Status?.Stream?.AckedOffset;
        var offset = (ulong?)desiredOffset;
        if (ackedOffset.HasValue && desiredOffset.HasValue)
        {
            if (this.Channel.Resource.Metadata.Generation > this.Channel.Resource.Status?.ObservedGeneration)
            {
                offset = (ulong)desiredOffset.Value;
                if (this.Channel.Resource.Status == null) this.Channel.Resource.Status = new() { ObservedGeneration = this.Channel.Resource.Metadata.Generation };
                if (this.Channel.Resource.Status.Stream == null) this.Channel.Resource.Status.Stream = new();
                this.Channel.Resource.Status.ObservedGeneration = this.Channel.Resource.Metadata.Generation;
                this.Channel.Resource.Status.Stream.AckedOffset = offset;
                await this.ResourceRepository.UpdateResourceStatusAsync(this.Channel.Resource, this.CancellationToken).ConfigureAwait(false);
            }
            else
            {
                offset = (ulong)ackedOffset;
            }
        }
        if (!offset.HasValue) offset = 0;
        if (this.Channel.Resource.Status == null)
        {
            this.Channel.Resource.Status = new() { ObservedGeneration = this.Channel.Resource.Metadata.Generation, Stream = new() { AckedOffset = offset.Value } };
            await this.ResourceRepository.UpdateResourceStatusAsync(this.Channel.Resource, stoppingToken).ConfigureAwait(false);
        }
        else if (this.Channel.Resource.Status.Stream == null)
        {
            this.Channel.Resource.Status!.Stream = new() { AckedOffset = offset.Value };
            await this.ResourceRepository.UpdateResourceStatusAsync(this.Channel.Resource, stoppingToken).ConfigureAwait(false);
        }
        else if (!this.Channel.Resource.Status.Stream.AckedOffset.HasValue)
        {
            this.Channel.Resource.Status!.Stream!.AckedOffset = offset.Value;
            await this.ResourceRepository.UpdateResourceStatusAsync(this.Channel.Resource, stoppingToken).ConfigureAwait(false);
        }
        foreach (var subscription in this.SubscriptionController.Resources.ToList())
        {
            await this.OnSubscriptionCreatedAsync(subscription).ConfigureAwait(false);
        }
        this.CloudEventStream.SubscribeAsync(this.OnCloudEventAsync, cancellationToken: stoppingToken);
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
    /// <param name="subcription">The newly created <see cref="Subscription"/></param>
    protected virtual async Task OnSubscriptionCreatedAsync(Subscription subcription)
    {
        var brokerOffset = this.Channel.Resource.Status!.Stream!.AckedOffset!.Value;
        var key = this.GetResourceCacheKey(subcription.GetName(), subcription.GetNamespace());
        var dispatcher = ActivatorUtilities.CreateInstance<SubscriptionCloudEventDispatcher>(this.ServiceProvider, subcription, brokerOffset);
        await dispatcher.InitializeAsync(this.CancellationToken).ConfigureAwait(false);
        this.Subscriptions.AddOrUpdate(key, dispatcher, (_, _) => dispatcher);
    }

    /// <summary>
    /// Handles the update of an existing <see cref="Subscription"/>
    /// </summary>
    /// <param name="subscription">The newly updated <see cref="Subscription"/></param>
    protected virtual async Task OnSubscriptionUpdatedAsync(Subscription subscription)
    {
        var brokerOffset = this.Channel.Resource.Status!.Stream!.AckedOffset!.Value;
        var key = this.GetResourceCacheKey(subscription.GetName(), subscription.GetNamespace());
        if (this.Subscriptions.TryGetValue(key, out var dispatcher) && dispatcher != null)
        {
            await dispatcher.SetSubscriptionAsync(subscription).ConfigureAwait(false);
            return;
        }
        dispatcher = ActivatorUtilities.CreateInstance<SubscriptionCloudEventDispatcher>(this.ServiceProvider, subscription, brokerOffset);
        this.Subscriptions.AddOrUpdate(key, dispatcher, (_, existing) =>
        {
            existing.Dispose();
            return dispatcher;
        });
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
    /// Dispatches the specified <see cref="CloudEvent"/>
    /// </summary>
    /// <param name="e">The <see cref="CloudEvent"/> to dispatch</param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    protected virtual async Task OnCloudEventAsync(CloudEvent e)
    {
        var tasks = new List<Task>(this.SubscriptionController.Resources.Count);
        foreach (var kvp in this.Subscriptions)
        {
            tasks.Add(kvp.Value.DispatchAsync(e));
        }
        await Task.WhenAll(tasks);
        this.Channel.Resource.Status!.Stream!.AckedOffset = e.GetSequence()!;
        await this.ResourceRepository.UpdateResourceStatusAsync(this.Channel.Resource, this.CancellationToken).ConfigureAwait(false);
    }

}
