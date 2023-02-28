using CloudStreams.Core.Infrastructure;
using CloudStreams.Core.Infrastructure.Services;
using FluentValidation;
using Polly;
using System.Net;
using System.Net.Mime;
using System.Reactive.Linq;
using System.Text.RegularExpressions;

namespace CloudStreams.Broker.Application.Services;

/// <summary>
/// Represents a service used to handle a <see cref="Subscription"/>
/// </summary>
public class SubscriptionHandler
{

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
    /// Gets the service used to evaluate runtime expressions
    /// </summary>
    protected IExpressionEvaluator ExpressionEvaluator { get; }

    /// <summary>
    /// Gets an <see cref="IEnumerable{T}"/> containing registered <see cref="CloudEvent"/> <see cref="IValidator"/>s
    /// </summary>
    protected IEnumerable<IValidator<CloudEvent>> CloudEventValidators { get; }

    /// <summary>
    /// Gets the service used to perform HTTP requests
    /// </summary>
    protected HttpClient HttpClient { get; }

    /// <summary>
    /// Gets the <see cref="Core.Data.Models.Subscription"/> to dispatch <see cref="CloudEvent"/>s to
    /// </summary>
    protected Subscription Subscription { get; private set; }

    /// <summary>
    /// Gets the <see cref="SubscriptionHandler"/>'s <see cref="System.Threading.CancellationTokenSource"/>
    /// </summary>
    protected CancellationTokenSource CancellationTokenSource { get; private set; } = null!;

    /// <summary>
    /// Gets the <see cref="SubscriptionHandler"/>'s <see cref="System.Threading.CancellationToken"/>
    /// </summary>
    protected CancellationToken CancellationToken => this.CancellationTokenSource.Token;

    /// <summary>
    /// Gets an <see cref="IObservable{T}"/> used to observe consumed <see cref="CloudEvent"/>s
    /// </summary>
    protected IObservable<CloudEvent> CloudEventStream { get; private set; } = null!;

    /// <summary>
    /// Gets a boolean indicating whether or not the subscriber is available
    /// </summary>
    protected bool SubscriberAvailable { get; private set; } = true;

    /// <summary>
    /// Gets a boolean indicating whether or not the subscription is faulted
    /// </summary>
    protected bool SubscriptionFaulted { get; set; }

    /// <summary>
    /// Gets the offset of the last acked <see cref="CloudEvent"/>
    /// </summary>
    protected ulong? AckedOffset => this.Subscription.Status?.Stream?.AckedOffset;

    /// <summary>
    /// Gets the offset of the last filtered <see cref="CloudEvent"/> in the stream
    /// </summary>
    protected ulong StreamOffset { get; private set; }

    /// <summary>
    /// Gets a boolean indicating whether or not the subscription is out of sync with the stream's last offset
    /// </summary>
    protected bool SubscriptionOutOfSync { get; set; }

    /// <summary>
    /// Initializes the <see cref="SubscriptionHandler"/>
    /// </summary>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    public virtual Task InitializeAsync(CancellationToken cancellationToken)
    {
        this.CancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        return this.InitializeCloudEventStreamAsync();
    }

    /// <summary>
    /// Initializes the <see cref="SubscriptionHandler"/>'s <see cref="CloudEventStream"/>
    /// </summary>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    protected virtual async Task InitializeCloudEventStreamAsync()
    {
        var offset = this.Subscription.GetOffset();
        this.Logger.LogDebug("Initializing the cloud event stream of subscription '{subscription}' at offset '{offset}'", this.Subscription, offset);
        if (this.Subscription.Spec.Partition == null) this.CloudEventStream = await this.EventStore.SubscribeAsync(offset, this.CancellationToken).ConfigureAwait(false);
        else this.CloudEventStream = await this.EventStore.SubscribeToPartitionAsync(this.Subscription.Spec.Partition, offset, this.CancellationToken).ConfigureAwait(false);
        this.CloudEventStream.Where(this.Filters).SubscribeAsync(this.OnCloudEventAsync, onErrorAsync: this.OnCloudEventStreamingError, cancellationToken: this.CancellationToken);
    }

    /// <summary>
    /// Determines whether or not the <see cref="Subscription"/> filters the specified <see cref="CloudEvent"/>
    /// </summary>
    /// <param name="e">The <see cref="CloudEvent"/> to filter</param>
    /// <returns>A boolean indicating whether or not the <see cref="Subscription"/> filters the specified <see cref="CloudEvent"/></returns>
    protected virtual bool Filters(CloudEvent e)
    {
        if(e == null) throw new ArgumentNullException(nameof(e));
        if (this.Subscription.Spec.Filter == null) return true;
        return this.Subscription.Spec.Filter.Strategy switch
        {
            CloudEventFilteringStrategy.Attributes => this.Filters(e, this.Subscription.Spec.Filter.Attributes!),
            CloudEventFilteringStrategy.Expression => this.ExpressionEvaluator.EvaluateCondition(this.Subscription.Spec.Filter.Expression!, e),
            _ => throw new NotSupportedException($"The specified {nameof(CloudEventFilteringStrategy)} '{EnumHelper.Stringify(this.Subscription.Spec.Filter.Strategy)}' is not supported")
        };
    }

    /// <summary>
    /// Determines whether or not the <see cref="Subscription"/> filters the specified <see cref="CloudEvent"/>
    /// </summary>
    /// <param name="e">The <see cref="CloudEvent"/> to filter</param>
    /// <param name="attributeFilters">An <see cref="IDictionary{TKey, TValue}"/> containing the key/value mappings of the attributes to filter the specified <see cref="CloudEvent"/> by</param>
    /// <returns>A boolean indicating whether or not the <see cref="Subscription"/> filters the specified <see cref="CloudEvent"/></returns>
    protected virtual bool Filters(CloudEvent e, IDictionary<string, string?> attributeFilters)
    {
        if (e == null) throw new ArgumentNullException(nameof(e));
        if (attributeFilters == null) throw new ArgumentNullException(nameof(attributeFilters));
        var attributes = e.ToDictionary<string?>()!;
        foreach (var attributeFilter in attributeFilters)
        {
            if (!attributes.TryGetValue(attributeFilter.Key, out var attributeValue) || string.IsNullOrWhiteSpace(attributeValue)) return false;
            if (string.IsNullOrWhiteSpace(attributeFilter.Value)) continue;
            if (attributeValue.IsRuntimeExpression() && !this.ExpressionEvaluator.EvaluateCondition(attributeFilter.Value, attributeValue)) return false;
            else if (!Regex.IsMatch(attributeValue, attributeFilter.Value)) return false;
        }
        return true;
    }

    /// <summary>
    /// Determines whether or not the <see cref="Subscription"/> filters the specified <see cref="CloudEvent"/>
    /// </summary>
    /// <param name="e">The <see cref="CloudEvent"/> to filter</param>
    /// <param name="expression">A runtime expression used to determine whether or not the specified <see cref="CloudEvent"/> should be dispatched to subscribers</param>
    /// <returns>A boolean indicating whether or not the <see cref="Subscription"/> filters the specified <see cref="CloudEvent"/></returns>
    protected virtual bool Filters(CloudEvent e, string expression)
    {
        if (e == null) throw new ArgumentNullException(nameof(e));
        if (string.IsNullOrWhiteSpace(expression)) throw new ArgumentNullException(nameof(expression));
        return this.ExpressionEvaluator.EvaluateCondition(expression, e);
    }

    /// <summary>
    /// Mutates the specified <see cref="CloudEvent"/>
    /// </summary>
    /// <param name="e">The <see cref="CloudEvent"/> to mutate</param>
    /// <returns>The mutated <see cref="CloudEvent"/></returns>
    protected virtual async Task<CloudEvent> MutateAsync(CloudEvent e)
    {
        if (e == null) throw new ArgumentNullException(nameof(e));
        if(this.Subscription.Spec.Mutation == null) return e.Clone()!;
        var mutated = this.Subscription.Spec.Mutation.Strategy switch
        {
            CloudEventMutationStrategy.Expression => await this.PerformExpressionBasedMutationAsync(this.Subscription.Spec.Mutation, e).ConfigureAwait(false),
            CloudEventMutationStrategy.Webhook => await this.PerformWebhookBasedMutationAsync(this.Subscription.Spec.Mutation, e).ConfigureAwait(false),
            _ => throw new NotSupportedException($"The specified {nameof(CloudEventMutationStrategy)} '{EnumHelper.Stringify(this.Subscription.Spec.Mutation.Strategy)}' is not supported"),
        };
        if (mutated == null) throw new NullReferenceException($"Failed to mutate the specified cloud event"); //todo: better feeback
        await this.ValidateAsync(mutated).ConfigureAwait(false);
        return mutated;
    }

    /// <summary>
    /// Performs a runtime expression based mutation on the specified <see cref="CloudEvent"/>
    /// </summary>
    /// <param name="mutation">An object used to configure the mutation to perform</param>
    /// <param name="e">The <see cref="CloudEvent"/> to mutate</param>
    /// <returns>The mutated <see cref="CloudEvent"/></returns>
    protected virtual Task<CloudEvent?> PerformExpressionBasedMutationAsync(CloudEventMutation mutation, CloudEvent e)
    {
        if (mutation == null) throw new ArgumentNullException(nameof(mutation));
        if (e == null) throw new ArgumentNullException(nameof(e));
        if (mutation.Strategy != CloudEventMutationStrategy.Expression) throw new FormatException(nameof(mutation.Strategy));
        if (mutation.Expression == null) throw new FormatException(nameof(mutation.Expression));
        if (mutation.Expression is string expression) return Task.FromResult(this.ExpressionEvaluator.Evaluate<CloudEvent>(expression, e));
        else return Task.FromResult(this.ExpressionEvaluator.Mutate<CloudEvent>(mutation.Expression, e));
    }

    /// <summary>
    /// Performs a webhook based mutation on the specified <see cref="CloudEvent"/>
    /// </summary>
    /// <param name="mutation">An object used to configure the mutation to perform</param>
    /// <param name="e">The <see cref="CloudEvent"/> to mutate</param>
    /// <returns>The mutated <see cref="CloudEvent"/></returns>
    protected virtual async Task<CloudEvent?> PerformWebhookBasedMutationAsync(CloudEventMutation mutation, CloudEvent e)
    {
        if (mutation == null) throw new ArgumentNullException(nameof(mutation));
        if (e == null) throw new ArgumentNullException(nameof(e));
        if (mutation.Strategy != CloudEventMutationStrategy.Webhook) throw new FormatException(nameof(mutation.Strategy));
        if (mutation.Webhook == null) throw new FormatException(nameof(mutation.Webhook));
        using var requestContent = e.ToHttpContent();
        using var request = new HttpRequestMessage(HttpMethod.Post, mutation.Webhook.ServiceUri) { Content = requestContent };
        request.Headers.Accept.Add(new(MediaTypeNames.Application.Json));
        using var response = await this.HttpClient.SendAsync(request, this.CancellationToken).ConfigureAwait(false);
        var responseContent = await response.Content.ReadAsStringAsync(this.CancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentType?.MediaType == MediaTypeNames.Application.Json) throw new Exception($"Unexpected HTTP response's content type: {response.Content.Headers.ContentType?.MediaType}"); //todo: better feedback
        return Serializer.Json.Deserialize<CloudEvent>(responseContent);
    }

    /// <summary>
    /// Validates the specified <see cref="CloudEvent"/>
    /// </summary>
    /// <param name="e">The <see cref="CloudEvent"/> to validate</param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    protected virtual async Task ValidateAsync(CloudEvent e)
    {
        if(e == null) throw new ArgumentNullException(nameof(e));
        var validationTasks = this.CloudEventValidators.Select(v => v.ValidateAsync(e, this.CancellationToken));
        await Task.WhenAll(validationTasks).ConfigureAwait(false);
        if (validationTasks.Any(t => !t.Result.IsValid)) throw new FormatException("Failed to validate the specified cloud event"); //todo: better feeback
    }

    /// <summary>
    /// Dispatches the specified <see cref="CloudEvent"/> to the configured subscriber
    /// </summary>
    /// <param name="e">The <see cref="CloudEvent"/> to dispatch</param>
    /// <param name="retryIfUnavailable">A boolean indicating whether or not to retry when the subscriber is unavailable</param>
    /// <param name="catchUpWhenAvailable">A boolean indicating whether or not to catch up missed events when the subscriber becomes available</param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    protected virtual async Task DispatchAsync(CloudEvent e, bool retryIfUnavailable, bool catchUpWhenAvailable)
    {
        if (e == null) throw new ArgumentNullException(nameof(e));
        var offset = e.GetSequence()!.Value;
        try
        {
            using var requestContent = e.ToHttpContent();
            using var request = new HttpRequestMessage(HttpMethod.Post, this.Subscription.Spec.Subscriber.ServiceUri) { Content = requestContent };
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
        catch(Exception ex)
        {

        }
        await this.CommitOffsetAsync(offset).ConfigureAwait(false);
    }

    /// <summary>
    /// Retries to dispatch the specified <see cref="CloudEvent"/>
    /// </summary>
    /// <param name="e">The <see cref="CloudEvent"/> to dispatch</param>
    /// <param name="catchUpWhenAvailable">A boolean indicating whether or not to catch up events when the subscribe becomes available again</param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    protected virtual async Task RetryDispatchAsync(CloudEvent e, bool catchUpWhenAvailable)
    {
        this.SubscriberAvailable = false;
        this.SubscriptionOutOfSync = true;

        var circuitBreakerPolicy = Policy.Handle<HttpRequestException>()
            .CircuitBreakerAsync(5, TimeSpan.FromSeconds(10));

        var retryPolicy = Policy.Handle<HttpRequestException>(ex => ex.StatusCode == HttpStatusCode.ServiceUnavailable)
            .RetryForeverAsync();

        await retryPolicy.WrapAsync(circuitBreakerPolicy)
            .ExecuteAsync(async _ => await this.DispatchAsync(e, false, catchUpWhenAvailable), this.CancellationToken).ConfigureAwait(false);

        this.SubscriberAvailable = true;
        if (catchUpWhenAvailable) await this.CatchUpAsync();
    }

    /// <summary>
    /// Catches up missed <see cref="CloudEvent"/>s
    /// </summary>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    protected virtual async Task CatchUpAsync()
    {
        this.SubscriptionOutOfSync = true;
        var currentOffset = this.AckedOffset;
        if (!currentOffset.HasValue)
        {
            var desiredOffset = this.Subscription.GetOffset();
            if (desiredOffset == StreamPosition.EndOfStream) desiredOffset = (long)(await this.EventStore.ReadOneAsync(StreamReadDirection.Backwards, StreamPosition.EndOfStream, this.CancellationToken).ConfigureAwait(false))?.GetSequence()!.Value!;
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
        while (!this.CancellationToken.IsCancellationRequested && currentOffset < (ulong)this.StreamOffset);
        this.SubscriptionOutOfSync = false;
    }

    /// <summary>
    /// Commits the specified offset
    /// </summary>
    /// <param name="offset">The <see cref="Subscription"/>'s offset to commit</param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    protected virtual async Task CommitOffsetAsync(ulong offset)
    {
        var resource = this.Subscription.Clone()!;
        if (resource.Status == null) resource.Status = new() { ObservedGeneration = this.Subscription.Metadata.Generation };
        if (resource.Status.Stream == null) resource.Status.Stream = new();
        resource.Status.Stream.AckedOffset = offset;
        await this.ResourceRepository.UpdateResourceStatusAsync(resource, this.CancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Handles updates to the managed <see cref="Subscription"/>'s state
    /// </summary>
    /// <param name="subscription">The updated <see cref="Core.Data.Models.Subscription"/></param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    protected virtual async Task OnSubscriptionChangedAsync(Subscription subscription)
    {
        try
        {

        }
        catch(Exception ex)
        {
            this.Logger.LogError("An error occured while watching updates to the handled subscription '{subscription}': {ex}", subscription, ex);
            this.SubscriptionFaulted = true;
        }
    }

    /// <summary>
    /// Handles the specified <see cref="CloudEvent"/>
    /// </summary>
    /// <param name="e">The <see cref="CloudEvent"/> to handle</param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    protected virtual async Task OnCloudEventAsync(CloudEvent e)
    {
        try
        {
            this.StreamOffset = e.GetSequence()!.Value;
            if (this.SubscriptionFaulted || !this.SubscriberAvailable || this.SubscriptionOutOfSync) return;
            var mutated = await this.MutateAsync(e).ConfigureAwait(false);
            await this.DispatchAsync(mutated, true, true).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            this.Logger.LogError("An error occured while handling the cloud event with id '{eventId}' for subscription '{subscription}': {ex}", e.Id, this.Subscription, ex);
            this.SubscriptionFaulted = true;
        }
    }

    /// <summary>
    /// Handles the specified unhandled <see cref="Exception"/>s that has been thrown during the streaming of filtered <see cref="CloudEvent"/>s
    /// </summary>
    /// <param name="ex">The unhandled <see cref="Exception"/> to handle</param>
    /// <returns>A new awaitable <see cref="Task"/>/returns>
    protected virtual Task OnCloudEventStreamingError(Exception ex)
    {
        this.Logger.LogError("An error occured while streaming cloud events for subscription '{subscription}': {ex}", this.Subscription, ex);
        this.SubscriptionFaulted = true;
        return Task.CompletedTask;
    }

}
