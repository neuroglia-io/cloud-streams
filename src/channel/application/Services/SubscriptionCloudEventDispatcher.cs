using CloudStreams.Core;
using Polly;
using System.Text.RegularExpressions;

namespace CloudStreams.Channel.Application.Services;

/// <summary>
/// Represents a service used to dispatch <see cref="CloudEvent"/>s to a <see cref="Core.Data.Models.Subscription"/>
/// </summary>
public class SubscriptionCloudEventDispatcher
    : IDisposable
{

    private bool _Disposed;

    /// <summary>
    /// Initializes a new <see cref="SubscriptionCloudEventDispatcher"/>
    /// </summary>
    /// <param name="loggerFactory">The service used to create <see cref="ILogger"/>s</param>
    /// <param name="resourceRepository">The service used to manage <see cref="IResource"/>s</param>
    /// <param name="expressionEvaluator">The service used to evaluate runtime expressions</param>
    /// <param name="httpClientFactory">The service used to create <see cref="System.Net.Http.HttpClient"/>s</param>
    /// <param name="subscription">The <see cref="Core.Data.Models.Subscription"/> to dispatch <see cref="CloudEvent"/>s to</param>
    /// <param name="brokerOffset">The broker's offset</param>
    public SubscriptionCloudEventDispatcher(ILoggerFactory loggerFactory, IResourceRepository resourceRepository, IExpressionEvaluator expressionEvaluator, IHttpClientFactory httpClientFactory, Subscription subscription, ulong brokerOffset)
    {
        this.Logger = loggerFactory.CreateLogger(this.GetType());
        this.ResourceRepository = resourceRepository;
        this.ExpressionEvaluator = expressionEvaluator;
        this.HttpClient = httpClientFactory.CreateClient();
        this.Subscription = subscription;
        this.ChannelOffset = brokerOffset;
    }

    /// <summary>
    /// Gets the service used to perform logging
    /// </summary>
    protected ILogger Logger { get; }

    /// <summary>
    /// Gets the service used to manage <see cref="IResource"/>s
    /// </summary>
    protected IResourceRepository ResourceRepository { get; }

    /// <summary>
    /// Gets the service used to evaluate runtime expressions
    /// </summary>
    protected IExpressionEvaluator ExpressionEvaluator { get; }

    /// <summary>
    /// Gets the service used to perform HTTP requests
    /// </summary>
    protected HttpClient HttpClient { get; }

    /// <summary>
    /// Gets the <see cref="Core.Data.Models.Subscription"/> to dispatch <see cref="CloudEvent"/>s to
    /// </summary>
    protected Subscription Subscription { get; private set; }

    /// <summary>
    /// Gets the <see cref="ChannelCloudEventDispatcher"/>'s <see cref="System.Threading.CancellationTokenSource"/>
    /// </summary>
    protected CancellationTokenSource CancellationTokenSource { get; private set; } = null!;

    /// <summary>
    /// Gets the <see cref="ChannelCloudEventDispatcher"/>'s <see cref="System.Threading.CancellationToken"/>
    /// </summary>
    protected CancellationToken CancellationToken => this.CancellationTokenSource.Token;

    /// <summary>
    /// Gets a boolean indicating whether or not the subscription service is online
    /// </summary>
    protected bool SubscriptionServiceAvailable { get; set; } = true;

    /// <summary>
    /// Gets a boolean indicating whether or not the subscription is out of sync with the channel
    /// </summary>
    protected bool SubscriptionOutOfSync { get; set; }

    /// <summary>
    /// Gets the offset of the channel
    /// </summary>
    protected ulong ChannelOffset { get; set; }

    /// <summary>
    /// Initializes the <see cref="SubscriptionCloudEventDispatcher"/>
    /// </summary>
    /// <param name="stoppingToken">A <see cref="System.Threading.CancellationToken"/> used to shutdown the <see cref="SubscriptionCloudEventDispatcher"/></param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    public virtual async Task InitializeAsync(CancellationToken stoppingToken)
    {
        this.CancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var desiredOffset = this.Subscription.Spec.Stream?.Offset;
        var ackedOffset = this.Subscription.Status?.Stream?.AckedOffset;
        if (!desiredOffset.HasValue || desiredOffset.Value == CloudEventStreamPosition.EndOfStream) desiredOffset = (long?)(await this.EventStore.ReadOneAsync(StreamReadDirection.Backwards, CloudEventStreamPosition.EndOfStream, stoppingToken).ConfigureAwait(false))?.GetSequence() + 1;
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
        if (offset < this.ChannelOffset) await this.CatchUpAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Sets the <see cref="Core.Data.Models.Subscription"/> to dispatch <see cref="CloudEvent"/>s to
    /// </summary>
    /// <param name="subscription">The <see cref="Core.Data.Models.Subscription"/> to dispatch <see cref="CloudEvent"/>s to</param>
    public virtual async Task SetSubscriptionAsync(Subscription subscription)
    {
        if (subscription == null) throw new ArgumentNullException(nameof(subscription));
        if (this.Subscription.Metadata.ResourceVersion == subscription.Metadata.ResourceVersion) return;
        var previousState = this.Subscription;
        this.Subscription = subscription;
        if (previousState.Metadata.Generation == subscription.Metadata.Generation) return;
        if (previousState.Spec.Stream?.Offset != this.Subscription.Spec.Stream?.Offset && (ulong?)this.Subscription.Spec.Stream?.Offset < this.ChannelOffset)
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
        this.ChannelOffset = e.GetSequence()!.Value;
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
            if(await this.FiltersAsync(e).ConfigureAwait(false))
            {
                var mutated = e;
                if (this.Subscription.Spec.Mutation != null) mutated = await this.MutateAsync(e).ConfigureAwait(false);
                using var content = mutated.ToHttpContent();
                using var request = new HttpRequestMessage(HttpMethod.Post, this.Subscription.Spec.ServiceAddress) { Content = content };
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
            }
            await this.CommitOffsetAsync(e.GetSequence()!.Value);
        }
        catch (Exception ex) when (ex is not HttpRequestException)
        {
            this.Logger.LogError("An error occured while dispatching the cloud event with id '{eventId}': {ex}", e.Id, ex);
            throw;
        }
    }

    protected virtual async Task<bool> FiltersAsync(CloudEvent e)
    {
        if(e == null) throw new ArgumentNullException(nameof(e));
        if (this.Subscription.Spec.Filter == null) return true;
        var attributes = e.ToDictionary()!.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString());
        var evalutationArguments = new Dictionary<string, object>() { { "CLOUDEVENT", e } };
        foreach(var attribute in this.Subscription.Spec.Filter.Attributes.ToList())
        {
            var matches = attributes.Where(a => Regex.IsMatch(a.Key, attribute.Key));
            if (string.IsNullOrWhiteSpace(attribute.Value)) continue;
            if (!matches.Any(a => attribute.Value.IsRuntimeExpression() ? this.ExpressionEvaluator.EvaluateCondition(attribute.Value, a.Value, evalutationArguments) : a.Value == attribute.Value)) return false;
        }
        return true;
    }

    protected virtual async Task<CloudEvent> MutateAsync(CloudEvent e)
    {
        if (e == null) throw new ArgumentNullException(nameof(e));
        if (this.Subscription.Spec.Mutation == null) return e;
        return this.ExpressionEvaluator.Evaluate<CloudEvent>(this.Subscription.Spec.Mutation, e);
    }

    protected virtual async Task CommitOffsetAsync(ulong offset)
    {

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

        var retryPolicy = Policy.Handle<HttpRequestException>()
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
            if (!desiredOffset.HasValue || desiredOffset.Value == CloudEventStreamPosition.EndOfStream) desiredOffset = (long?)(await this.EventStore.ReadOneAsync(StreamReadDirection.Backwards, CloudEventStreamPosition.EndOfStream, this.CancellationToken).ConfigureAwait(false))?.GetSequence();
            currentOffset = (ulong?)desiredOffset;
        }
        do
        {
            var e = await this.EventStore.ReadOneAsync(StreamReadDirection.Forwards, (long)currentOffset!, this.CancellationToken).ConfigureAwait(false);
            if (e == null)
            {
                await Task.Delay(50);
                continue;
            }
            await this.DispatchAsync(e, true, false).ConfigureAwait(false);
            currentOffset++;
        }
        while (!this.CancellationToken.IsCancellationRequested && currentOffset < (ulong)this.ChannelOffset);
        this.SubscriptionOutOfSync = false;
    }

    /// <summary>
    /// Disposes of the <see cref="SubscriptionCloudEventDispatcher"/>
    /// </summary>
    /// <param name="disposing">A boolean indicating whether or not the <see cref="SubscriptionCloudEventDispatcher"/> is being disposed of</param>
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
