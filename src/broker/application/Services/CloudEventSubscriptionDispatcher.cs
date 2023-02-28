using CloudStreams.Core.Infrastructure;
using CloudStreams.Core.Infrastructure.Services;
using Polly;
using System.Net;

namespace CloudStreams.Broker.Application.Services;

/// <summary>
/// Represents a service used to dispatch <see cref="CloudEvent"/>s to a <see cref="Core.Data.Models.Subscription"/>
/// </summary>
public class CloudEventSubscriptionDispatcher
    : IDisposable
{

    private bool _Disposed;

    /// <summary>
    /// Initializes a new <see cref="CloudEventSubscriptionDispatcher"/>
    /// </summary>
    /// <param name="loggerFactory">The service used to create <see cref="ILogger"/>s</param>
    /// <param name="eventStore">The service used to store <see cref="CloudEvent"/>s</param>
    /// <param name="resourceRepository">The service used to manage <see cref="IResource"/>s</param>
    /// <param name="httpClientFactory">The service used to create <see cref="System.Net.Http.HttpClient"/>s</param>
    /// <param name="subscription">The <see cref="Core.Data.Models.Subscription"/> to dispatch <see cref="CloudEvent"/>s to</param>
    /// <param name="brokerOffset">The broker's offset</param>
    public CloudEventSubscriptionDispatcher(ILoggerFactory loggerFactory, ICloudEventStore eventStore, IResourceRepository resourceRepository, IHttpClientFactory httpClientFactory, Subscription subscription, ulong brokerOffset)
    {
        this.Logger = loggerFactory.CreateLogger(this.GetType());
        this.EventStore = eventStore;
        this.ResourceRepository = resourceRepository;
        this.HttpClient = httpClientFactory.CreateClient();
        this.Subscription = subscription;
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
    /// Gets the <see cref="Core.Data.Models.Subscription"/> to dispatch <see cref="CloudEvent"/>s to
    /// </summary>
    protected Subscription Subscription { get; private set; }

    /// <summary>
    /// Gets the <see cref="CloudEventSubscriptionDispatcher"/>'s <see cref="System.Threading.CancellationTokenSource"/>
    /// </summary>
    protected CancellationTokenSource CancellationTokenSource { get; private set; } = null!;

    /// <summary>
    /// Gets the <see cref="CloudEventSubscriptionDispatcher"/>'s <see cref="System.Threading.CancellationToken"/>
    /// </summary>
    protected CancellationToken CancellationToken => this.CancellationTokenSource.Token;

    /// <summary>
    /// Gets a boolean indicating whether or not the subscription service is online
    /// </summary>
    protected bool SubscriptionServiceAvailable { get; set; } = true;

    /// <summary>
    /// Gets a boolean indicating whether or not the subscription is out of sync with the broker
    /// </summary>
    protected bool SubscriptionOutOfSync { get; set; }

    /// <summary>
    /// Gets the offset of the broker
    /// </summary>
    protected ulong BrokerOffset { get; set; }

    /// <summary>
    /// Initializes the <see cref="CloudEventSubscriptionDispatcher"/>
    /// </summary>
    /// <param name="stoppingToken">A <see cref="System.Threading.CancellationToken"/> used to shutdown the <see cref="CloudEventSubscriptionDispatcher"/></param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    public virtual async Task InitializeAsync(CancellationToken stoppingToken)
    {
        this.CancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var desiredOffset = this.Subscription.Spec.Stream?.Offset;
        var ackedOffset = this.Subscription.Status?.Stream?.AckedOffset;
        if (!desiredOffset.HasValue || desiredOffset.Value == StreamPosition.EndOfStream) desiredOffset = (long?)(await this.EventStore.ReadOneAsync(StreamReadDirection.Backwards, StreamPosition.EndOfStream, stoppingToken).ConfigureAwait(false))?.GetSequence() + 1;
        var offset = (ulong?)desiredOffset;
        if (ackedOffset.HasValue)
        {
            if (this.Subscription.Metadata.Generation > this.Subscription.Status?.ObservedGeneration)
            {
                var resource = this.Subscription.Clone()!;
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
        if (this.Subscription.Status == null)
        {
            var resource = this.Subscription.Clone()!;
            resource.Status = new() { ObservedGeneration = resource.Metadata.Generation, Stream = new() { AckedOffset = offset } };
            await this.ResourceRepository.UpdateResourceStatusAsync(resource, stoppingToken).ConfigureAwait(false);
        }
        else if (this.Subscription.Status.Stream == null)
        {
            var resource = this.Subscription.Clone()!;
            resource.Status!.Stream = new() { AckedOffset = offset };
            await this.ResourceRepository.UpdateResourceStatusAsync(resource, stoppingToken).ConfigureAwait(false);
        }
        else if (!this.Subscription.Status.Stream.AckedOffset.HasValue)
        {
            var resource = this.Subscription.Clone()!;
            resource.Status!.Stream!.AckedOffset = offset;
            await this.ResourceRepository.UpdateResourceStatusAsync(resource, stoppingToken).ConfigureAwait(false);
        }
        if (offset < this.BrokerOffset) await this.CatchUpAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Sets the <see cref="Core.Data.Models.Subscription"/> to dispatch <see cref="CloudEvent"/>s to
    /// </summary>
    /// <param name="subscription">The <see cref="Core.Data.Models.Subscription"/> to dispatch <see cref="CloudEvent"/>s to</param>
    public virtual async Task SetSubscriptionAsync(Subscription subscription)
    {
        if(subscription == null) throw new ArgumentNullException(nameof(subscription));
        if (this.Subscription.Metadata.ResourceVersion == subscription.Metadata.ResourceVersion) return;
        var previousState = this.Subscription;
        this.Subscription = subscription;
        if (previousState.Metadata.Generation == subscription.Metadata.Generation) return;
        if (previousState.Spec.Stream?.Offset != this.Subscription.Spec.Stream?.Offset 
            && (ulong?)this.Subscription.Spec.Stream?.Offset < this.BrokerOffset)
        {
            var resource = this.Subscription.Clone()!;
            resource.Status = new() { Stream = new() { AckedOffset = (ulong?)this.Subscription.Spec.Stream?.Offset } };
            this.Subscription = await this.ResourceRepository.UpdateResourceStatusAsync(resource, this.CancellationToken).ConfigureAwait(false);
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
        this.BrokerOffset = e.GetSequence()!.Value;
        if (!this.SubscriptionServiceAvailable || this.SubscriptionOutOfSync) return Task.CompletedTask;
        return this.DispatchAsync(e, true, true);
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
            using var request = new HttpRequestMessage(HttpMethod.Post, this.Subscription.Spec.Subscriber.ServiceUri) { Content = content };
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
            var resource = this.Subscription.Clone()!;
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
        this.SubscriptionServiceAvailable = false;
        this.SubscriptionOutOfSync = true;

        var circuitBreakerPolicy = Policy.Handle<HttpRequestException>()
            .CircuitBreakerAsync(5, TimeSpan.FromSeconds(10));

        var retryPolicy = Policy.Handle<HttpRequestException>(ex => ex.StatusCode == HttpStatusCode.ServiceUnavailable)
            .RetryForeverAsync();

        await retryPolicy.WrapAsync(circuitBreakerPolicy)
            .ExecuteAsync(async _ => await this.DispatchAsync(e, false, catchUpWhenAvailable), this.CancellationToken).ConfigureAwait(false);

        this.SubscriptionServiceAvailable = true;
        if (catchUpWhenAvailable) await this.CatchUpAsync();
    }

    /// <summary>
    /// Catches up missed <see cref="CloudEvent"/>s
    /// </summary>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    protected virtual async Task CatchUpAsync()
    {
        this.SubscriptionOutOfSync = true;
        ulong? currentOffset = this.Subscription.Status?.Stream?.AckedOffset;
        if (!currentOffset.HasValue)
        {
            var desiredOffset = this.Subscription.Spec.Stream?.Offset;
            if (!desiredOffset.HasValue || desiredOffset.Value == StreamPosition.EndOfStream) desiredOffset = (long?)(await this.EventStore.ReadOneAsync(StreamReadDirection.Backwards, StreamPosition.EndOfStream, this.CancellationToken).ConfigureAwait(false))?.GetSequence();
            currentOffset = (ulong?)desiredOffset;
        }
        do
        {
            var e = await this.EventStore.ReadOneAsync(StreamReadDirection.Forwards, (long)currentOffset!, this.CancellationToken).ConfigureAwait(false);
            if(e == null)
            {
                await Task.Delay(50);
                continue;
            }
            await this.DispatchAsync(e, true, false).ConfigureAwait(false);
            currentOffset++;
        }
        while (!this.CancellationToken.IsCancellationRequested && currentOffset < (ulong)this.BrokerOffset);
        this.SubscriptionOutOfSync = false;
    }

    /// <summary>
    /// Disposes of the <see cref="CloudEventDispatcher"/>
    /// </summary>
    /// <param name="disposing">A boolean indicating whether or not the <see cref="CloudEventDispatcher"/> is being disposed of</param>
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
