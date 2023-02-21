using CloudStreams.Core.Infrastructure;
using CloudStreams.Core.Infrastructure.Services;
using Polly;
using System.Net;

namespace CloudStreams.Broker.Application.Services;

/// <summary>
/// Represents a service used to dispatch <see cref="CloudEvent"/>s to a <see cref="Core.Data.Models.Channel"/>
/// </summary>
public class ChannelCloudEventDispatcher
    : IDisposable
{

    private bool _Disposed;

    /// <summary>
    /// Initializes a new <see cref="ChannelCloudEventDispatcher"/>
    /// </summary>
    /// <param name="loggerFactory">The service used to create <see cref="ILogger"/>s</param>
    /// <param name="eventStore">The service used to store <see cref="CloudEvent"/>s</param>
    /// <param name="resourceRepository">The service used to manage <see cref="IResource"/>s</param>
    /// <param name="httpClientFactory">The service used to create <see cref="System.Net.Http.HttpClient"/>s</param>
    /// <param name="channel">The <see cref="Core.Data.Models.Channel"/> to dispatch <see cref="CloudEvent"/>s to</param>
    /// <param name="brokerOffset">The broker's offset</param>
    public ChannelCloudEventDispatcher(ILoggerFactory loggerFactory, ICloudEventStore eventStore, IResourceRepository resourceRepository, IHttpClientFactory httpClientFactory, Channel channel, ulong brokerOffset)
    {
        this.Logger = loggerFactory.CreateLogger(this.GetType());
        this.EventStore = eventStore;
        this.ResourceRepository = resourceRepository;
        this.HttpClient = httpClientFactory.CreateClient();
        this.Channel = channel;
        this.BrokerOffset = brokerOffset;
    }

    /// <summary>
    /// Gets the service used to perform logging
    /// </summary>
    protected ILogger Logger { get; }

    /// <summary>
    /// Gets the service used to store <see cref="CloudEvent"/>s
    /// </summary>
    protected ICloudEventStore EventStore { get; }

    /// <summary>
    /// Gets the service used to manage <see cref="IResource"/>s
    /// </summary>
    protected IResourceRepository ResourceRepository { get; }

    /// <summary>
    /// Gets the service used to perform HTTP requests
    /// </summary>
    protected HttpClient HttpClient { get; }

    /// <summary>
    /// Gets the <see cref="Core.Data.Models.Channel"/> to dispatch <see cref="CloudEvent"/>s to
    /// </summary>
    protected Channel Channel { get; private set; }

    /// <summary>
    /// Gets the <see cref="ChannelCloudEventDispatcher"/>'s <see cref="System.Threading.CancellationTokenSource"/>
    /// </summary>
    protected CancellationTokenSource CancellationTokenSource { get; private set; } = null!;

    /// <summary>
    /// Gets the <see cref="ChannelCloudEventDispatcher"/>'s <see cref="System.Threading.CancellationToken"/>
    /// </summary>
    protected CancellationToken CancellationToken => this.CancellationTokenSource.Token;

    /// <summary>
    /// Gets a boolean indicating whether or not the channel service is online
    /// </summary>
    protected bool ChannelServiceAvailable { get; set; } = true;

    /// <summary>
    /// Gets a boolean indicating whether or not the channel is out of sync with the broker
    /// </summary>
    protected bool ChannelOutOfSync { get; set; }

    /// <summary>
    /// Gets the offset of the broker
    /// </summary>
    protected ulong BrokerOffset { get; set; }

    /// <summary>
    /// Initializes the <see cref="ChannelCloudEventDispatcher"/>
    /// </summary>
    /// <param name="stoppingToken">A <see cref="System.Threading.CancellationToken"/> used to shutdown the <see cref="ChannelCloudEventDispatcher"/></param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    public virtual async Task InitializeAsync(CancellationToken stoppingToken)
    {
        this.CancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var desiredOffset = this.Channel.Spec.Stream?.Offset;
        var ackedOffset = this.Channel.Status?.Stream?.AckedOffset;
        if (!desiredOffset.HasValue || desiredOffset.Value == CloudEventStreamPosition.EndOfStream) desiredOffset = (long?)(await this.EventStore.ReadOneAsync(StreamReadDirection.Backwards, CloudEventStreamPosition.EndOfStream, stoppingToken).ConfigureAwait(false))?.GetSequence() + 1;
        var offset = (ulong?)desiredOffset;
        if (ackedOffset.HasValue)
        {
            if (this.Channel.Metadata.Generation > this.Channel.Status?.ObservedGeneration)
            {
                var resource = this.Channel.Clone()!;
                resource.Status ??= new() { ObservedGeneration = resource.Metadata.Generation };
                if (resource.Status.Stream == null) resource.Status.Stream = new();
                resource.Status.ObservedGeneration = resource.Metadata.Generation;
                resource.Status.Stream.AckedOffset = offset;
                await this.ResourceRepository.UpdateResourceStatusAsync(resource, this.CancellationToken).ConfigureAwait(false);
            }
            else
            {
                offset = (ulong)ackedOffset + 1;
            }
        }
        if (this.Channel.Status == null)
        {
            var resource = this.Channel.Clone()!;
            resource.Status = new() { ObservedGeneration = resource.Metadata.Generation, Stream = new() { AckedOffset = offset.Value } };
            await this.ResourceRepository.UpdateResourceStatusAsync(resource, stoppingToken).ConfigureAwait(false);
        }
        else if (this.Channel.Status.Stream == null)
        {
            var resource = this.Channel.Clone()!;
            resource.Status!.Stream = new() { AckedOffset = offset.Value };
            await this.ResourceRepository.UpdateResourceStatusAsync(resource, stoppingToken).ConfigureAwait(false);
        }
        else if (!this.Channel.Status.Stream.AckedOffset.HasValue)
        {
            var resource = this.Channel.Clone()!;
            resource.Status!.Stream!.AckedOffset = offset.Value;
            await this.ResourceRepository.UpdateResourceStatusAsync(resource, stoppingToken).ConfigureAwait(false);
        }
        if (offset < this.BrokerOffset) await this.CatchUpAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Sets the <see cref="Core.Data.Models.Channel"/> to dispatch <see cref="CloudEvent"/>s to
    /// </summary>
    /// <param name="channel">The <see cref="Core.Data.Models.Channel"/> to dispatch <see cref="CloudEvent"/>s to</param>
    public virtual async Task SetChannelAsync(Channel channel)
    {
        if(channel == null) throw new ArgumentNullException(nameof(channel));
        if (this.Channel.Metadata.ResourceVersion == channel.Metadata.ResourceVersion) return;
        var previousState = this.Channel;
        this.Channel = channel;
        if (previousState.Metadata.Generation == channel.Metadata.Generation) return;
        if (previousState.Spec.Stream?.Offset != this.Channel.Spec.Stream?.Offset 
            && (ulong?)this.Channel.Spec.Stream?.Offset < this.BrokerOffset)
        {
            var resource = this.Channel.Clone()!;
            resource.Status = new() { Stream = new() { AckedOffset = (ulong?)this.Channel.Spec.Stream?.Offset } };
            this.Channel = await this.ResourceRepository.UpdateResourceStatusAsync(resource, this.CancellationToken).ConfigureAwait(false);
            await this.CatchUpAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Dispatches the specified <see cref="CloudEvent"/>
    /// </summary>
    /// <param name="e">The <see cref="CloudEvent"/> to dispatch</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public virtual Task DispatchAsync(CloudEvent e, CancellationToken cancellationToken = default)
    {
        try
        {
            this.BrokerOffset = e.GetSequence()!.Value;
            if (!this.ChannelServiceAvailable || this.ChannelOutOfSync) return Task.CompletedTask;
            return this.DispatchAsync(e, true, true);
        }
        catch(Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Dispatches the specified <see cref="CloudEvent"/>
    /// </summary>
    /// <param name="e">The <see cref="CloudEvent"/> to dispatch</param>
    /// <param name="retryIfUnavailable">A boolean indicating whether or not to retry if the service is unavailable</param>
    /// <param name="catchUpWhenAvailable">A boolean indicating whether or not to catch up events when the service is again available</param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    protected virtual async Task DispatchAsync(CloudEvent e, bool retryIfUnavailable, bool catchUpWhenAvailable)
    {
        try
        {
            using var content = e.ToHttpContent();
            using var request = new HttpRequestMessage(HttpMethod.Post, this.Channel.Spec.ServiceAddress) { Content = content };
            using var response = await this.HttpClient.SendAsync(request, this.CancellationToken).ConfigureAwait(false);
            if (retryIfUnavailable)
            {
                if (response.IsSuccessStatusCode) return;
                if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
                {
                    _ = this.RetryDispatchAsync(e, catchUpWhenAvailable);
                    return;
                }
            }
            else
            {
                response.EnsureSuccessStatusCode();
            }
            var resource = this.Channel.Clone()!;
            resource.Status ??= new();
            if (resource.Status.Stream == null) resource.Status.Stream = new();
        }
        catch(Exception ex) when (ex is not HttpRequestException)
        {
            this.Logger.LogError("An error occured while dispatching the cloud event with id '{eventId}': {ex}", e.Id, ex);
            throw;
        }
    }

    /// <summary>
    /// Retry to dispatch the specified <see cref="CloudEvent"/>
    /// </summary>
    /// <param name="e">The <see cref="CloudEvent"/> to dispatch</param>
    /// <param name="catchUpWhenAvailable">A boolean indicating whether or not to catch up events when the service is again available</param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    protected virtual async Task RetryDispatchAsync(CloudEvent e, bool catchUpWhenAvailable)
    {
        this.ChannelServiceAvailable = false;
        this.ChannelOutOfSync = true;

        var circuitBreakerPolicy = Policy.Handle<HttpRequestException>()
            .CircuitBreakerAsync(5, TimeSpan.FromSeconds(10));

        var retryPolicy = Policy.Handle<HttpRequestException>()
            .RetryForeverAsync();

        await retryPolicy.WrapAsync(circuitBreakerPolicy)
            .ExecuteAsync(async _ => await this.DispatchAsync(e, false, catchUpWhenAvailable), this.CancellationToken).ConfigureAwait(false);

        if (catchUpWhenAvailable) await this.CatchUpAsync();

        this.ChannelServiceAvailable = true;
    }

    /// <summary>
    /// Catches up missed <see cref="CloudEvent"/>s
    /// </summary>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    protected virtual async Task CatchUpAsync()
    {
        this.ChannelOutOfSync = true;
        ulong? currentOffset = this.Channel.Status?.Stream?.AckedOffset;
        if (!currentOffset.HasValue)
        {
            var desiredOffset = this.Channel.Spec.Stream?.Offset;
            if (!desiredOffset.HasValue || desiredOffset.Value == CloudEventStreamPosition.EndOfStream) desiredOffset = (long?)(await this.EventStore.ReadOneAsync(StreamReadDirection.Backwards, CloudEventStreamPosition.EndOfStream, this.CancellationToken).ConfigureAwait(false))?.GetSequence();
            currentOffset = (ulong?)desiredOffset;
        }
        do
        {
            var e = await this.EventStore.ReadOneAsync(StreamReadDirection.Forwards, (long)currentOffset.Value, this.CancellationToken).ConfigureAwait(false);
            if(e == null)
            {
                await Task.Delay(50);
                continue;
            }
            await this.DispatchAsync(e, true, false).ConfigureAwait(false);
            currentOffset++;
        }
        while (!this.CancellationToken.IsCancellationRequested && currentOffset < (ulong)this.BrokerOffset);
        this.ChannelOutOfSync = false;
    }

    /// <summary>
    /// Disposes of the <see cref="BrokerCloudEventDispatcher"/>
    /// </summary>
    /// <param name="disposing">A boolean indicating whether or not the <see cref="BrokerCloudEventDispatcher"/> is being disposed of</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!this._Disposed)
        {
            if (disposing)
            {
                this.HttpClient.Dispose();
                this.CancellationTokenSource.Dispose();
            }
            this._Disposed = true;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

}
