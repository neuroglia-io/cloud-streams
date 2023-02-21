using CloudStreams.Broker.Application.Configuration;
using CloudStreams.Core.Infrastructure;
using CloudStreams.Core.Infrastructure.Services;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Reactive.Linq;

namespace CloudStreams.Broker.Application.Services;

/// <summary>
/// Represents the service used to dispatch consumed <see cref="CloudEvent"/>s to controlled <see cref="Channel"/>s
/// </summary>
public class BrokerCloudEventDispatcher
    : BackgroundService
{

    /// <summary>
    /// Initializes a new <see cref="BrokerCloudEventDispatcher"/>
    /// </summary>
    /// <param name="serviceProvider">The current <see cref="IServiceProvider"/></param>
    /// <param name="loggerFactory">The service used to create <see cref="ILogger"/>s</param>
    /// <param name="brokerOptions">The service used to access the current <see cref="Configuration.BrokerOptions"/></param>
    /// <param name="eventStore">The service used to store <see cref="CloudEvent"/>s</param>
    /// <param name="resources">The service used to manage <see cref="IResource"/>s</param>
    /// <param name="channelController">The service used to control <see cref="Channel"/> resources</param>
    public BrokerCloudEventDispatcher(IServiceProvider serviceProvider, ILoggerFactory loggerFactory, IOptions<BrokerOptions> brokerOptions, ICloudEventStore eventStore, IResourceRepository resources, IResourceController<Channel> channelController)
    {
        this.ServiceProvider = serviceProvider;
        this.Logger = loggerFactory.CreateLogger(this.GetType());
        this.BrokerOptions = brokerOptions.Value;
        this.EventStore = eventStore;
        this.Resources = resources;
        this.ChannelController = channelController;
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
    /// Gets the object used to configure the current Cloud Streams cloud event broker
    /// </summary>
    protected BrokerOptions BrokerOptions { get; }

    /// <summary>
    /// Gets the service used to store <see cref="CloudEvent"/>s
    /// </summary>
    protected ICloudEventStore EventStore { get; }

    /// <summary>
    /// Gets the service used to manage <see cref="IResource"/>s
    /// </summary>
    protected IResourceRepository Resources { get; }

    /// <summary>
    /// Gets the service used to control <see cref="Channel"/> resources
    /// </summary>
    protected IResourceController<Channel> ChannelController { get; }

    /// <summary>
    /// Gets the service used to monitor the broker's configuration
    /// </summary>
    protected IResourceMonitor<Core.Data.Models.Broker> Broker { get; private set; } = null!;

    /// <summary>
    /// Gets an <see cref="IObservable{T}"/> used to observe <see cref="CloudEvent"/>s consumed by the <see cref="BrokerCloudEventDispatcher"/>
    /// </summary>
    protected IObservable<CloudEvent> EventStream { get; private set; } = null!;

    /// <summary>
    /// Gets a <see cref="ConcurrentDictionary{TKey, TValue}"/> used to cache active <see cref="Channel"/>s
    /// </summary>
    protected ConcurrentDictionary<string, ChannelCloudEventDispatcher> Channels { get; } = new();

    /// <summary>
    /// Gets the <see cref="BrokerCloudEventDispatcher"/>'s <see cref="System.Threading.CancellationTokenSource"/>
    /// </summary>
    protected CancellationTokenSource CancellationTokenSource { get; private set; } = null!;

    /// <summary>
    /// Gets the <see cref="BrokerCloudEventDispatcher"/>'s <see cref="System.Threading.CancellationToken"/>
    /// </summary>
    protected CancellationToken CancellationToken => this.CancellationTokenSource.Token;

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        this.CancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        this.Broker = await this.Resources.MonitorAsync<Core.Data.Models.Broker>(this.BrokerOptions.Name, this.BrokerOptions.Namespace, cancellationToken: this.CancellationToken);
        var desiredOffset = this.Broker.Resource.Spec.Stream?.Offset;
        var ackedOffset = this.Broker.Resource.Status?.Stream?.AckedOffset;
        if (!desiredOffset.HasValue || desiredOffset.Value == CloudEventStreamPosition.EndOfStream) desiredOffset = (long?)(await this.EventStore.ReadOneAsync(StreamReadDirection.Backwards, CloudEventStreamPosition.EndOfStream, stoppingToken).ConfigureAwait(false))?.GetSequence() + 1;
        var offset = (ulong?)desiredOffset;
        if (ackedOffset.HasValue && desiredOffset.HasValue)
        {
            if(this.Broker.Resource.Metadata.Generation > this.Broker.Resource.Status?.ObservedGeneration)
            {
                offset = (ulong)desiredOffset.Value;
                if (this.Broker.Resource.Status == null) this.Broker.Resource.Status = new() { ObservedGeneration = this.Broker.Resource.Metadata.Generation };
                if (this.Broker.Resource.Status.Stream == null) this.Broker.Resource.Status.Stream = new();
                this.Broker.Resource.Status.ObservedGeneration = this.Broker.Resource.Metadata.Generation;
                this.Broker.Resource.Status.Stream.AckedOffset = offset;
                await this.Resources.UpdateResourceStatusAsync(this.Broker.Resource, this.CancellationToken).ConfigureAwait(false);
            }
            else
            {
                offset = (ulong)ackedOffset;
            }
        }
        if (!offset.HasValue) offset = 0;
        if (this.Broker.Resource.Status == null)
        {
            this.Broker.Resource.Status = new() { ObservedGeneration = this.Broker.Resource.Metadata.Generation, Stream = new() { AckedOffset = offset.Value } };
            await this.Resources.UpdateResourceStatusAsync(this.Broker.Resource, stoppingToken).ConfigureAwait(false);
        }
        else if(this.Broker.Resource.Status.Stream == null)
        {
            this.Broker.Resource.Status!.Stream = new() { AckedOffset = offset.Value };
            await this.Resources.UpdateResourceStatusAsync(this.Broker.Resource, stoppingToken).ConfigureAwait(false);
        }
        else if (!this.Broker.Resource.Status.Stream.AckedOffset.HasValue)
        {
            this.Broker.Resource.Status!.Stream!.AckedOffset = offset.Value;
            await this.Resources.UpdateResourceStatusAsync(this.Broker.Resource, stoppingToken).ConfigureAwait(false);
        }
        foreach (var channel in this.ChannelController.Resources.ToList())
        {
            this.OnChannelCreated(channel);
        }
        this.EventStream = await this.EventStore.SubscribeAsync((long)offset.Value, cancellationToken: this.CancellationToken).ConfigureAwait(false);
        this.EventStream.SubscribeAsync(OnCloudEventAsync, this.CancellationToken);
        await Task.Delay(50);
        this.ChannelController.Where(e => e.Type == ResourceWatchEventType.Created).Select(e => e.Resource).Subscribe(this.OnChannelCreated, stoppingToken);
        this.ChannelController.Where(e => e.Type == ResourceWatchEventType.Updated).Select(e => e.Resource).Subscribe(this.OnChannelUpdated, stoppingToken);
        this.ChannelController.Where(e => e.Type == ResourceWatchEventType.Deleted).Select(e => e.Resource).Subscribe(this.OnChannelDeleted, stoppingToken);
    }

    /// <summary>
    /// Builds a new cache key for the specified resource
    /// </summary>
    /// <param name="name">The name of the resource to build a new cache key for</param>
    /// <param name="namespace">The namespace the resource to build a new cache key for belongs to</param>
    /// <returns>A new cache key</returns>
    protected virtual string GetResourceCacheKey(string name, string? @namespace) => string.IsNullOrWhiteSpace(@namespace) ? name : $"{@namespace}.{name}";

    /// <summary>
    /// Handles the creation of a new <see cref="Channel"/>
    /// </summary>
    /// <param name="channel">The newly created <see cref="Channel"/></param>
    protected virtual async void OnChannelCreated(Channel channel)
    {
        var brokerOffset = this.Broker.Resource.Status!.Stream!.AckedOffset!.Value;
        var key = this.GetResourceCacheKey(channel.GetName(), channel.GetNamespace());
        var dispatcher = ActivatorUtilities.CreateInstance<ChannelCloudEventDispatcher>(this.ServiceProvider, channel, brokerOffset);
        await dispatcher.InitializeAsync(this.CancellationToken).ConfigureAwait(false);
        this.Channels.AddOrUpdate(key, dispatcher, (_, _) => dispatcher);
    }

    /// <summary>
    /// Handles the update of an existing <see cref="Channel"/>
    /// </summary>
    /// <param name="channel">The newly updated <see cref="Channel"/></param>
    protected virtual void OnChannelUpdated(Channel channel)
    {
        var brokerOffset = this.Broker.Resource.Status!.Stream!.AckedOffset!.Value;
        var key = this.GetResourceCacheKey(channel.GetName(), channel.GetNamespace());
        if (this.Channels.TryGetValue(key, out var dispatcher) && dispatcher != null)
        {
            dispatcher.SetChannelAsync(channel);
            return;
        }
        dispatcher = ActivatorUtilities.CreateInstance<ChannelCloudEventDispatcher>(this.ServiceProvider, channel, brokerOffset);
        this.Channels.AddOrUpdate(key, dispatcher, (_, existing) =>
        {
            existing.Dispose();
            return dispatcher;
        });
    }

    /// <summary>
    /// Handles the deletion of a new <see cref="Channel"/>
    /// </summary>
    /// <param name="channel">The newly deleted <see cref="Channel"/></param>
    protected virtual void OnChannelDeleted(Channel channel)
    {
        var key = this.GetResourceCacheKey(channel.GetName(), channel.GetNamespace());
        if (!this.Channels.Remove(key, out var dispatcher) || dispatcher == null) return;
        dispatcher.Dispose();
    }

    /// <summary>
    /// Dispatches the specified <see cref="CloudEvent"/>
    /// </summary>
    /// <param name="e">The <see cref="CloudEvent"/> to dispatch</param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    protected virtual async Task OnCloudEventAsync(CloudEvent e)
    {
        var tasks = new List<Task>(this.ChannelController.Resources.Count);
        foreach(var kvp in this.Channels)
        {
            tasks.Add(kvp.Value.DispatchAsync(e));
        }
        await Task.WhenAll(tasks);
        this.Broker.Resource.Status!.Stream!.AckedOffset = e.GetSequence()!;
        await this.Resources.UpdateResourceStatusAsync(this.Broker.Resource, this.CancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        this.Broker?.Dispose();
        this.CancellationTokenSource?.Dispose();
        base.Dispose();
        GC.SuppressFinalize(this);
    }

}
