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

using FluentValidation;
using Grpc.Core;
using Polly;
using Polly.CircuitBreaker;
using System.Numerics;
using System.Reactive.Threading.Tasks;
using System.Text.RegularExpressions;

namespace CloudStreams.Broker.Application.Services;

/// <summary>
/// Represents a service used to handle a <see cref="Core.Resources.Subscription"/>
/// </summary>
public class SubscriptionHandler
{

    static class StreamInitializationOutcomes
    {
        public const string Initialized = "initialized";
        public const string Failed = "failed";
        public const string Canceled = "canceled";
        public const string Superseded = "superseded";
        public const string StreamNotFoundRetry = "stream_not_found_retry";
    }

    bool _disposed;
    long _streamInitializationSessionId;

    /// <summary>
    /// Initializes a new <see cref="SubscriptionHandler"/>
    /// </summary>
    /// <param name="loggerFactory">The service used to create <see cref="ILogger"/>s</param>
    /// <param name="hostApplicationLifetime">The service used to manage the application's lifetime</param>
    /// <param name="serializer">The service used to serialize/deserialize data to/from JSON</param>
    /// <param name="cloudEventStore">The service used to store <see cref="CloudEvent"/>s</param>
    /// <param name="resourceRepository">The service used to manage <see cref="IResource"/>s</param>
    /// <param name="subscriptionController">The service used to control <see cref="Core.Resources.Subscription"/> resources</param>
    /// <param name="broker">The service used to monitor the current <see cref="Core.Resources.Broker"/></param>
    /// <param name="expressionEvaluator">The service used to evaluate runtime expressions</param>
    /// <param name="cloudEventValidators">An <see cref="IEnumerable{T}"/> containing registered <see cref="CloudEvent"/> <see cref="IValidator"/>s</param>
    /// <param name="httpClient">The service used to perform HTTP requests</param>
    /// <param name="metrics">The service used to track broker metrics</param>
    /// <param name="subscription">The <see cref="Core.Resources.Subscription"/> to dispatch <see cref="CloudEvent"/>s to</param>
    public SubscriptionHandler(ILoggerFactory loggerFactory, IHostApplicationLifetime hostApplicationLifetime, IJsonSerializer serializer, ICloudEventStore cloudEventStore, IResourceRepository resourceRepository, IResourceController<Subscription> subscriptionController,
        IResourceMonitor<Core.Resources.Broker> broker, IExpressionEvaluator expressionEvaluator, IEnumerable<IValidator<CloudEvent>> cloudEventValidators, HttpClient httpClient, IBrokerMetrics metrics, Subscription subscription)
    {
        this.Logger = loggerFactory.CreateLogger(this.GetType());
        this.HostApplicationLifetime = hostApplicationLifetime;
        this.Serializer = serializer;
        this.EventStore = cloudEventStore;
        this.ResourceRepository = resourceRepository;
        this.SubscriptionController = subscriptionController;
        this.Broker = broker;
        this.ExpressionEvaluator = expressionEvaluator;
        this.CloudEventValidators = cloudEventValidators;
        this.HttpClient = httpClient;
        this.Metrics = metrics;
        this.Subscription = subscription;
        this.DefaultRetryPolicy = this.Broker.Resource.Spec.Dispatch?.RetryPolicy ?? new HttpClientRetryPolicy();
        hostApplicationLifetime.ApplicationStopping.Register(async () => await this.SetStatusPhaseAsync(SubscriptionStatusPhase.Inactive).ConfigureAwait(false));
    }

    /// <summary>
    /// Gets the service used to perform logging
    /// </summary>
    protected ILogger Logger { get; }

    /// <summary>
    /// Gets the service used to manage the application's lifetime
    /// </summary>
    protected IHostApplicationLifetime HostApplicationLifetime { get; }

    /// <summary>
    /// Gets the service used to serialize/deserialize data to/from JSON
    /// </summary>
    protected IJsonSerializer Serializer { get; }

    /// <summary>
    /// Gets the service used to stream <see cref="CloudEvent"/>s
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
    /// Gets the service used to control <see cref="Core.Resources.Subscription"/> resources
    /// </summary>
    protected IResourceController<Subscription> SubscriptionController { get; }

    /// <summary>
    /// Gets the service used to monitor the current <see cref="Core.Resources.Broker"/>
    /// </summary>
    protected IResourceMonitor<Core.Resources.Broker> Broker { get; }

    /// <summary>
    /// Gets an <see cref="IEnumerable{T}"/> containing registered <see cref="CloudEvent"/> <see cref="IValidator"/>s
    /// </summary>
    protected IEnumerable<IValidator<CloudEvent>> CloudEventValidators { get; }

    /// <summary>
    /// Gets the service used to perform HTTP requests
    /// </summary>
    protected HttpClient HttpClient { get; }

    /// <summary>
    /// Gets the service used to track broker metrics
    /// </summary>
    protected IBrokerMetrics Metrics { get; }

    /// <summary>
    /// Gets the <see cref="Core.Resources.Subscription"/> to dispatch <see cref="CloudEvent"/>s to
    /// </summary>
    protected Subscription Subscription { get; private set; }

    /// <summary>
    /// Gets the default <see cref="HttpClientRetryPolicy"/>
    /// </summary>
    protected HttpClientRetryPolicy DefaultRetryPolicy { get; private set; }

    /// <summary>
    /// Gets the offset of the last filtered <see cref="CloudEvent"/> in the stream
    /// </summary>
    protected ulong? StreamOffset { get; private set; }

    /// <summary>
    /// Gets an <see cref="IObservable{T}"/> used to observe consumed <see cref="CloudEvent"/>s
    /// </summary>
    protected IObservable<CloudEventRecord>? CloudEventStream { get; private set; }

    /// <summary>
    /// Gets the <see cref="SubscriptionHandler"/>'s <see cref="System.Threading.CancellationTokenSource"/>
    /// </summary>
    protected CancellationTokenSource CancellationTokenSource { get; private set; } = null!;

    /// <summary>
    /// Gets the <see cref="System.Threading.CancellationTokenSource"/> used to cancel an ongoing stream synchronization loop
    /// </summary>
    protected CancellationTokenSource? StreamInitializationCancellationTokenSource { get; private set; }

    /// <summary>
    /// Gets the <see cref="TaskCompletionSource"/> used to await the completion of an ongoing stream synchronization loop
    /// </summary>
    protected TaskCompletionSource? StreamSynchronizationTaskCompletionSource { get; private set; }

    /// <summary>
    /// Gets an <see cref="IDisposable"/> used to dispose of the active <see cref="CloudEvent"/> subscription, if any
    /// </summary>
    protected IDisposable? SubscriptionHandle { get; private set; }

    /// <summary>
    /// Gets the <see cref="SemaphoreSlim"/> used to lock the initialization process and ensures a maximum of one active subscription at all times
    /// </summary>
    protected SemaphoreSlim InitLock { get; } = new(1, 1);

    /// <summary>
    /// Gets a boolean indicating whether or not the subscription is out of sync with the stream's last offset
    /// </summary>
    protected bool SubscriptionOutOfSync { get; set; }

    /// <summary>
    /// Gets the last offset requested through subscription updates
    /// </summary>
    protected long? LastRequestedOffset { get; set; }

    /// <summary>
    /// Initializes the <see cref="SubscriptionHandler"/>
    /// </summary>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    public virtual async Task InitializeAsync(CancellationToken cancellationToken)
    {
        using var activity = CloudStreamsDefaults.Telemetry.ActivitySource.StartActivity("SubscriptionHandler.Initialize");
        this.CancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        this.LastRequestedOffset = this.Subscription.Spec.Stream?.Offset;
        this.SubscriptionController.Where(e => e.Type == ResourceWatchEventType.Updated && e.Resource.GetName() == this.Subscription.GetName() && e.Resource.GetNamespace() == this.Subscription.GetNamespace()).Select(e => e.Resource)
            .Select(subscription =>
            {
                this.Subscription = subscription;
                return subscription;
            })
            .Publish(shared => Observable.Merge(
                shared.Where(subscription => subscription.Spec.Stream != null)
                    .Select(subscription => subscription.Spec.Stream!.Offset)
                    .DistinctUntilChanged()
                    .Select(offset => OnSubscriptionOffsetChangedAsync(offset).ToObservable()),
                shared.Where(s => s.Status != null && s.Status.Stream != null)
                    .Select(s => s.Status!.Stream!.Fault)
                    .DistinctUntilChanged()
                    .Where(fault => fault != null)
                    .Select(fault => OnSubscriptionStreamingFaultAsync(fault).ToObservable())
            ))
            .Subscribe(this.CancellationTokenSource.Token);
        this.Broker
            .Select(e => e.Resource.Spec.Dispatch?.RetryPolicy)
            .DistinctUntilChanged()
            .Subscribe(policy => this.DefaultRetryPolicy = policy ?? new HttpClientRetryPolicy(), this.CancellationTokenSource.Token);
        await this.SetStatusPhaseAsync(SubscriptionStatusPhase.Active).ConfigureAwait(false);
        _ = this.InitializeCloudEventStreamAsync();
    }

    /// <summary>
    /// Initializes the <see cref="SubscriptionHandler"/>
    /// </summary>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    protected virtual async Task InitializeCloudEventStreamAsync()
    {
        using var activity = CloudStreamsDefaults.Telemetry.ActivitySource.StartActivity("SubscriptionHandler.InitializeStream");
        if (this.Subscription.Status?.Stream?.Fault != null) return;
        var initializationAttemptId = Guid.NewGuid().ToString("N");
        var initializationOutcome = StreamInitializationOutcomes.Failed;
        var streamInitializationSessionId = 0L;
        CancellationTokenSource? streamInitializationCancellationTokenSource = null;
        CancellationTokenSource? previousStreamInitializationCancellationTokenSource = null;
        Task? previousStreamSynchronizationTask = null;
        IDisposable? previousSubscriptionHandle = null;
        long offset = StreamPosition.EndOfStream;
        try
        {
            if (this.Subscription.Metadata.Generation > this.Subscription.Status?.ObservedGeneration
                && this.Subscription.Spec.Stream?.Offset != (long?)this.Subscription.Status?.Stream?.AckedOffset)
            {
                await this.CommitOffsetAsync(null).ConfigureAwait(false);
            }

            await this.InitLock.WaitAsync(this.CancellationTokenSource.Token).ConfigureAwait(false);
            try
            {
                previousSubscriptionHandle = this.SubscriptionHandle;
                this.SubscriptionHandle = null;
                previousStreamInitializationCancellationTokenSource = this.StreamInitializationCancellationTokenSource;
                previousStreamSynchronizationTask = this.StreamSynchronizationTaskCompletionSource?.Task;
                this.StreamSynchronizationTaskCompletionSource = null;
                streamInitializationCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(this.CancellationTokenSource.Token);
                this.StreamInitializationCancellationTokenSource = streamInitializationCancellationTokenSource;
                streamInitializationSessionId = Interlocked.Increment(ref this._streamInitializationSessionId);
            }
            finally { this.InitLock.Release(); }

            previousStreamInitializationCancellationTokenSource?.Cancel();
            if (previousStreamSynchronizationTask != null)
            {
                try
                {
                    await previousStreamSynchronizationTask.ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is OperationCanceledException or TaskCanceledException || (ex is RpcException rpcException && rpcException.StatusCode == StatusCode.Cancelled)) { }
            }
            previousSubscriptionHandle?.Dispose();
            previousStreamInitializationCancellationTokenSource?.Dispose();

            var streamInitializationToken = streamInitializationCancellationTokenSource.Token;
            offset = this.Subscription.GetOffset();
            IObservable<CloudEventRecord>? observedStream = null;
            this.Logger.LogDebug("Initializing the cloud event stream of subscription '{subscription}' at offset '{offset}' (attempt: {attemptId}, session: {sessionId})", this.Subscription, offset, initializationAttemptId, streamInitializationSessionId);
            if (this.Subscription.Spec.Partition == null)
            {
                while (!streamInitializationToken.IsCancellationRequested)
                {
                    try
                    {
                        observedStream = await this.EventStore.ObserveAsync(offset, streamInitializationToken).ConfigureAwait(false);
                        this.StreamOffset = (await this.EventStore.GetStreamMetadataAsync(streamInitializationToken).ConfigureAwait(false)).Length;
                        if (offset >= 0 && (ulong)offset == this.StreamOffset) offset = -1;
                        break;
                    }
                    catch (StreamNotFoundException) when (!streamInitializationToken.IsCancellationRequested)
                    {
                        initializationOutcome = StreamInitializationOutcomes.StreamNotFoundRetry;
                        var delay = 5000;
                        this.Logger.LogDebug("Failed to observe the cloud event stream because the first cloud event is yet to be published. Retrying in {delay} milliseconds... (attempt: {attemptId}, session: {sessionId})", delay, initializationAttemptId, streamInitializationSessionId);
                        await Task.Delay(delay, streamInitializationToken).ConfigureAwait(false);
                    }
                }
            }
            else
            {
                while (!streamInitializationToken.IsCancellationRequested)
                {
                    try
                    {
                        observedStream = await this.EventStore.ObservePartitionAsync(this.Subscription.Spec.Partition, offset, streamInitializationToken).ConfigureAwait(false);
                        this.StreamOffset = (await this.EventStore.GetPartitionMetadataAsync(this.Subscription.Spec.Partition, streamInitializationToken).ConfigureAwait(false)).Length;
                        if (offset >= 0 && (ulong)offset == this.StreamOffset) offset = -1;
                        break;
                    }
                    catch (StreamNotFoundException) when (!streamInitializationToken.IsCancellationRequested)
                    {
                        initializationOutcome = StreamInitializationOutcomes.StreamNotFoundRetry;
                        var delay = 5000;
                        this.Logger.LogDebug("Failed to observe the cloud event stream because the first cloud event is yet to be published. Retrying in {delay} milliseconds... (attempt: {attemptId}, session: {sessionId})", delay, initializationAttemptId, streamInitializationSessionId);
                        await Task.Delay(delay, streamInitializationToken).ConfigureAwait(false);
                    }
                }
            }
            if (streamInitializationToken.IsCancellationRequested)
            {
                initializationOutcome = StreamInitializationOutcomes.Canceled;
                return;
            }
            if (observedStream == null)
            {
                initializationOutcome = StreamInitializationOutcomes.Failed;
                return;
            }
            if (!this.IsStreamInitializationSessionCurrent(streamInitializationSessionId))
            {
                initializationOutcome = StreamInitializationOutcomes.Superseded;
                return;
            }
            var subscriptionHandle = observedStream.ToAsyncEnumerable().WhereAwait(this.FiltersAsync).ToObservable().SubscribeAsync(this.OnCloudEventAsync, onErrorAsync: this.OnSubscriptionErrorAsync, null);
            var shouldCatchUp = false;
            await this.InitLock.WaitAsync(this.CancellationTokenSource.Token).ConfigureAwait(false);
            try
            {
                if (streamInitializationToken.IsCancellationRequested)
                {
                    initializationOutcome = StreamInitializationOutcomes.Canceled;
                }
                else if (!this.IsStreamInitializationSessionCurrent(streamInitializationSessionId))
                {
                    initializationOutcome = StreamInitializationOutcomes.Superseded;
                }
                else
                {
                    this.CloudEventStream = observedStream;
                    this.SubscriptionHandle = subscriptionHandle;
                    shouldCatchUp = this.Subscription.Status?.ObservedGeneration == null || (offset != StreamPosition.EndOfStream && (ulong)offset < this.StreamOffset);
                    initializationOutcome = StreamInitializationOutcomes.Initialized;
                    subscriptionHandle = null;
                }
            }
            finally { this.InitLock.Release(); }
            subscriptionHandle?.Dispose();
            if (initializationOutcome == StreamInitializationOutcomes.Initialized && shouldCatchUp) _ = this.CatchUpAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is OperationCanceledException or TaskCanceledException || (ex is RpcException rpcException && rpcException.StatusCode == StatusCode.Cancelled))
        {
            initializationOutcome = this.IsStreamInitializationSessionCurrent(streamInitializationSessionId) ? StreamInitializationOutcomes.Canceled : StreamInitializationOutcomes.Superseded;
        }
        catch (Exception ex)
        {
            if (streamInitializationSessionId > 0 && !this.IsStreamInitializationSessionCurrent(streamInitializationSessionId))
            {
                initializationOutcome = StreamInitializationOutcomes.Superseded;
                return;
            }
            await this.OnSubscriptionErrorAsync(ex).ConfigureAwait(false);
        }
        finally
        {
            this.Logger.LogDebug("Cloud event stream initialization attempt '{attemptId}' for subscription '{subscription}' completed with outcome '{outcome}' (session: {sessionId})", initializationAttemptId, this.Subscription.GetQualifiedName(), initializationOutcome, streamInitializationSessionId);
        }
    }

    /// <summary>
    /// Determines whether or not the specified stream initialization session is the current one
    /// </summary>
    /// <param name="sessionId">The id of the stream initialization session to check</param>
    /// <returns>A boolean indicating whether or not the specified stream initialization session is the current one</returns>
    protected virtual bool IsStreamInitializationSessionCurrent(long sessionId)
    {
        return sessionId > 0 && Interlocked.Read(ref this._streamInitializationSessionId) == sessionId;
    }

    /// <summary>
    /// Determines whether or not the <see cref="Core.Resources.Subscription"/> filters the specified <see cref="CloudEventRecord"/>
    /// </summary>
    /// <param name="e">The <see cref="CloudEventRecord"/> to filter</param>
    /// <returns>A boolean indicating whether or not the <see cref="Core.Resources.Subscription"/> filters the specified <see cref="CloudEventRecord"/></returns>
    protected virtual ValueTask<bool> FiltersAsync(CloudEventRecord e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (this.Subscription.Spec.Filter == null) return ValueTask.FromResult(true);
        return this.Subscription.Spec.Filter.Type switch
        {
            CloudEventFilterType.Attributes => this.FiltersAsync(e, this.Subscription.Spec.Filter.Attributes!),
            CloudEventFilterType.Expression => this.FiltersAsync(e, this.Subscription.Spec.Filter.Expression!),
            _ => throw new NotSupportedException($"The specified {nameof(CloudEventFilterType)} '{EnumHelper.Stringify(this.Subscription.Spec.Filter.Type)}' is not supported")
        };
    }

    /// <summary>
    /// Determines whether or not the <see cref="Core.Resources.Subscription"/> filters the specified <see cref="CloudEventRecord"/>
    /// </summary>
    /// <param name="e">The <see cref="CloudEventRecord"/> to filter</param>
    /// <param name="attributeFilters">An <see cref="IDictionary{TKey, TValue}"/> containing the key/value mappings of the attributes to filter the specified <see cref="CloudEventRecord"/> by</param>
    /// <returns>A boolean indicating whether or not the <see cref="Core.Resources.Subscription"/> filters the specified <see cref="CloudEventRecord"/></returns>
    protected virtual async ValueTask<bool> FiltersAsync(CloudEventRecord e, IDictionary<string, string?> attributeFilters)
    {
        ArgumentNullException.ThrowIfNull(e);
        ArgumentNullException.ThrowIfNull(attributeFilters);
        var attributes = e.Metadata.ContextAttributes.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString());
        foreach (var attributeFilter in attributeFilters)
        {
            if (!attributes.TryGetValue(attributeFilter.Key, out var attributeValue) || string.IsNullOrWhiteSpace(attributeValue)) return false;
            if (string.IsNullOrWhiteSpace(attributeFilter.Value)) continue;
            if (attributeValue.IsRuntimeExpression() && !await this.ExpressionEvaluator.EvaluateConditionAsync(attributeFilter.Value, attributeValue, cancellationToken: this.CancellationTokenSource.Token).ConfigureAwait(false)) return false;
            else if (!Regex.IsMatch(attributeValue, attributeFilter.Value)) return false;
        }
        return true;
    }

    /// <summary>
    /// Determines whether or not the <see cref="Core.Resources.Subscription"/> filters the specified <see cref="CloudEvent"/>
    /// </summary>
    /// <param name="e">The <see cref="CloudEvent"/> to filter</param>
    /// <param name="expression">A runtime expression used to determine whether or not the specified <see cref="CloudEvent"/> should be dispatched to subscribers</param>
    /// <returns>A boolean indicating whether or not the <see cref="Core.Resources.Subscription"/> filters the specified <see cref="CloudEvent"/></returns>
    protected virtual async ValueTask<bool> FiltersAsync(CloudEventRecord e, string expression)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (string.IsNullOrWhiteSpace(expression)) throw new ArgumentNullException(nameof(expression));
        return await this.ExpressionEvaluator.EvaluateConditionAsync(expression, e, cancellationToken: this.CancellationTokenSource.Token).ConfigureAwait(false);
    }

    /// <summary>
    /// Mutates the specified <see cref="CloudEvent"/>
    /// </summary>
    /// <param name="e">The <see cref="CloudEvent"/> to mutate</param>
    /// <returns>The mutated <see cref="CloudEvent"/></returns>
    protected virtual async Task<CloudEvent> MutateAsync(CloudEvent e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (this.Subscription.Spec.Mutation == null) return e.Clone()!;
        var mutated = this.Subscription.Spec.Mutation.Type switch
        {
            CloudEventMutationType.Expression => await this.MutateAsync(e, this.Subscription.Spec.Mutation.Expression!).ConfigureAwait(false),
            CloudEventMutationType.Webhook => await this.MutateAsync(e, this.Subscription.Spec.Mutation.Webhook!).ConfigureAwait(false),
            _ => throw new NotSupportedException($"The specified {nameof(CloudEventMutationType)} '{EnumHelper.Stringify(this.Subscription.Spec.Mutation.Type)}' is not supported"),
        } ?? throw new NullReferenceException("Failed to mutate the specified cloud event");
        await this.ValidateAsync(mutated).ConfigureAwait(false);
        return mutated;
    }

    /// <summary>
    /// Performs a runtime expression based mutation on the specified <see cref="CloudEvent"/>
    /// </summary>
    /// <param name="e">The <see cref="CloudEvent"/> to mutate</param>
    /// <param name="mutation">An object used to configure the mutation to perform</param>
    /// <returns>The mutated <see cref="CloudEvent"/></returns>
    protected virtual async Task<CloudEvent?> MutateAsync(CloudEvent e, object mutation)
    {
        ArgumentNullException.ThrowIfNull(e);
        ArgumentNullException.ThrowIfNull(mutation);
        if (mutation is string expression) return await this.ExpressionEvaluator.EvaluateAsync<CloudEvent>(expression, e);
        else return await this.ExpressionEvaluator.EvaluateAsync<CloudEvent>(mutation, e);
    }

    /// <summary>
    /// Performs a webhook based mutation on the specified <see cref="CloudEvent"/>
    /// </summary>
    /// <param name="e">The <see cref="CloudEvent"/> to mutate</param>
    /// <param name="webhook">An object used to configure the webhook to invoke</param>
    /// <returns>The mutated <see cref="CloudEvent"/></returns>
    protected virtual async Task<CloudEvent?> MutateAsync(CloudEvent e, Webhook webhook)
    {
        ArgumentNullException.ThrowIfNull(e);
        ArgumentNullException.ThrowIfNull(webhook);
        using var requestContent = e.ToHttpContent();
        using var request = new HttpRequestMessage(HttpMethod.Post, webhook.ServiceUri) { Content = requestContent };
        request.Headers.Accept.Add(new(MediaTypeNames.Application.Json));
        using var response = await this.HttpClient.SendAsync(request, this.CancellationTokenSource.Token).ConfigureAwait(false);
        var responseContent = await response.Content.ReadAsStringAsync(this.CancellationTokenSource.Token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentType?.MediaType != MediaTypeNames.Application.Json) throw new Exception($"Unexpected HTTP response's content type: {response.Content.Headers.ContentType?.MediaType}");
        return this.Serializer.Deserialize<CloudEvent>(responseContent);
    }

    /// <summary>
    /// Validates the specified <see cref="CloudEvent"/>
    /// </summary>
    /// <param name="e">The <see cref="CloudEvent"/> to validate</param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    protected virtual async Task ValidateAsync(CloudEvent e)
    {
        ArgumentNullException.ThrowIfNull(e);
        using var activity = CloudStreamsDefaults.Telemetry.ActivitySource.StartActivity("SubscriptionHandler.Validate");
        activity?.SetTag("subscription", this.Subscription.GetQualifiedName());
        activity?.SetTag("event.id", e.Id);
        activity?.SetTag("event.source", e.Source);
        activity?.SetTag("event.type", e.Type);
        var validationTasks = this.CloudEventValidators.Select(v => v.ValidateAsync(e, this.CancellationTokenSource.Token));
        await Task.WhenAll(validationTasks).ConfigureAwait(false);
        if (validationTasks.Any(t => !t.Result.IsValid)) throw new FormatException("Failed to validate the specified cloud event"); //todo: better feedback
    }

    /// <summary>
    /// Dispatches the specified <see cref="CloudEventRecord"/> to the configured subscriber
    /// </summary>
    /// <param name="e">The <see cref="CloudEventRecord"/> to dispatch</param>
    /// <param name="retryOnError">A boolean indicating whether or not to retry when the subscriber is unavailable</param>
    /// <param name="catchUpWhenAvailable">A boolean indicating whether or not to catch up missed events when the subscriber becomes available</param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    protected virtual async Task DispatchAsync(CloudEventRecord e, bool retryOnError, bool catchUpWhenAvailable)
    {
        using var activity = CloudStreamsDefaults.Telemetry.ActivitySource.StartActivity("SubscriptionHandler.DispatchRecord");
        activity?.SetTag("subscription", this.Subscription.GetQualifiedName());
        activity?.SetTag("record.stream.id", e.StreamId);
        activity?.SetTag("record.sequence", e.Sequence);
        if (e.Metadata.ContextAttributes.TryGetValue(CloudEventAttributes.Id, out var id)) activity?.SetTag("event.id", id.ToString());
        if (e.Metadata.ContextAttributes.TryGetValue(CloudEventAttributes.Source, out var source)) activity?.SetTag("event.source", source.ToString());
        if (e.Metadata.ContextAttributes.TryGetValue(CloudEventAttributes.Type, out var type)) activity?.SetTag("event.type", type.ToString());
        if (e.Metadata.ContextAttributes.TryGetValue(CloudEventAttributes.Subject, out var subject)) activity?.SetTag("event.subject", subject.ToString());
        activity?.SetTag("retry", retryOnError);
        activity?.SetTag("catchup", catchUpWhenAvailable);
        ArgumentNullException.ThrowIfNull(e);
        var cloudEvent = e.ToCloudEvent(this.Broker.Resource.Spec.Dispatch?.Sequencing);
        if (!await this.FiltersAsync(e).ConfigureAwait(false)) return;
        cloudEvent = await this.MutateAsync(cloudEvent).ConfigureAwait(false);
        await this.DispatchAsync(cloudEvent, e.Sequence, retryOnError, catchUpWhenAvailable).ConfigureAwait(false);
    }

    /// <summary>
    /// Dispatches the specified <see cref="CloudEvent"/> to the configured subscriber
    /// </summary>
    /// <param name="e">The <see cref="CloudEvent"/> to dispatch</param>
    /// <param name="offset">The offset of the <see cref="CloudEvent"/> to dispatch</param>
    /// <param name="retryOnError">A boolean indicating whether or not to retry when the subscriber is unavailable</param>
    /// <param name="catchUpWhenAvailable">A boolean indicating whether or not to catch up missed events when the subscriber becomes available</param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    protected virtual async Task DispatchAsync(CloudEvent e, ulong offset, bool retryOnError, bool catchUpWhenAvailable)
    {
        try
        {
            using var requestContent = e.ToHttpContent();
            using var request = new HttpRequestMessage(HttpMethod.Post, this.Subscription.Spec.Subscriber.Uri) { Content = requestContent };
            using var response = await this.HttpClient.SendAsync(request, this.CancellationTokenSource.Token).ConfigureAwait(false);
            if (retryOnError && !response.IsSuccessStatusCode)
            {
                var reason = $"The server responded with a non-success status code '{response.StatusCode}'";
                var responseContent = await response.Content.ReadAsStringAsync(this.CancellationTokenSource.Token).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(responseContent)) reason = $"{reason}: {responseContent}";
                await this.RetryDispatchAsync(e, offset, catchUpWhenAvailable, reason).ConfigureAwait(false);
            }
            else
            {
                response.EnsureSuccessStatusCode();
                var streamName = this.Subscription.Spec.Partition != null ? this.Subscription.Spec.Partition.GetStreamName() : "All";
                var subscriberUri = this.Subscription.Spec.Subscriber.Uri.ToString();
                this.Metrics.IncrementTotalPublishedEvents(streamName, subscriberUri);
                if (this.Subscription.Spec.Stream != null) await this.CommitOffsetAsync(offset + 1).ConfigureAwait(false);
                if (this.Subscription.Spec.Subscriber.RateLimit.HasValue) await Task.Delay((int)(1000 / this.Subscription.Spec.Subscriber.RateLimit.Value), this.CancellationTokenSource.Token).ConfigureAwait(false);
            }
        }
        catch (HttpRequestException ex)
        {
            var reason = ex.InnerException == null 
                ? $"The server responded with a non-success status code '{ex.StatusCode}'"
                : ex.InnerException.Message;
            await this.RetryDispatchAsync(e, offset, catchUpWhenAvailable, reason).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Retries to dispatch the specified <see cref="CloudEvent"/>
    /// </summary>
    /// <param name="e">The <see cref="CloudEvent"/> to dispatch</param>
    /// <param name="offset">The offset of the <see cref="CloudEvent"/> to dispatch</param>
    /// <param name="catchUpWhenAvailable">A boolean indicating whether or not to catch up events when the subscribe becomes available again</param>
    /// <param name="statusReason">The reason why the initial dispatch attempt has failed</param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    protected virtual async Task RetryDispatchAsync(CloudEvent e, ulong offset, bool catchUpWhenAvailable, string statusReason)
    {
        using var activity = CloudStreamsDefaults.Telemetry.ActivitySource.StartActivity("SubscriptionHandler.RetryDispatch");
        activity?.SetTag("subscription", this.Subscription.GetQualifiedName());
        activity?.SetTag("event.id", e.Id);
        activity?.SetTag("event.source", e.Source);
        activity?.SetTag("event.type", e.Type);
        if (!string.IsNullOrWhiteSpace(e.Subject)) activity?.SetTag("event.subject", e.Subject);
        activity?.SetTag("offset", offset);
        activity?.SetTag("catchup", catchUpWhenAvailable);
        activity?.SetTag("reason", statusReason);
        try
        {
            await this.SetSubscriberStateAsync(SubscriberState.Unreachable, statusReason).ConfigureAwait(false);
            var streamName = this.Subscription.Spec.Partition != null ? this.Subscription.Spec.Partition.GetStreamName() : "All";
            var subscriberUri = this.Subscription.Spec.Subscriber.Uri.ToString();
            this.Metrics.IncrementTotalDeliveryFailures(streamName, subscriberUri);
            this.SubscriptionOutOfSync = true;
            var policyConfiguration = this.Subscription.Spec.Subscriber.RetryPolicy ?? this.DefaultRetryPolicy;
            bool exceptionPredicate(HttpRequestException ex) => policyConfiguration.StatusCodes == null || policyConfiguration.StatusCodes.Count == 0 || (ex.StatusCode.HasValue && ex.StatusCode.HasValue && policyConfiguration.StatusCodes.Contains((int)ex.StatusCode.Value));

            AsyncCircuitBreakerPolicy? circuitBreakerPolicy = policyConfiguration.CircuitBreaker == null ? null : Policy.Handle((Func<HttpRequestException, bool>)exceptionPredicate)
                .CircuitBreakerAsync(policyConfiguration.CircuitBreaker.BreakAfter, policyConfiguration.CircuitBreaker.BreakDuration.ToTimeSpan());

            AsyncPolicy retryPolicy = policyConfiguration.MaxAttempts.HasValue ?
                Policy.Handle((Func<HttpRequestException, bool>)exceptionPredicate)
                    .WaitAndRetryAsync(policyConfiguration.MaxAttempts.Value, attempt => policyConfiguration.BackoffDuration == null ? TimeSpan.FromSeconds(3) : policyConfiguration.BackoffDuration.ForAttempt(attempt))
                : Policy.Handle((Func<HttpRequestException, bool>)exceptionPredicate)
                    .WaitAndRetryForeverAsync(attempt => policyConfiguration.BackoffDuration == null ? TimeSpan.FromSeconds(3) : policyConfiguration.BackoffDuration.ForAttempt(attempt));

            retryPolicy = circuitBreakerPolicy == null ? retryPolicy : retryPolicy.WrapAsync(circuitBreakerPolicy);
            await retryPolicy.ExecuteAsync(async _ => await this.DispatchAsync(e, offset, false, catchUpWhenAvailable), this.CancellationTokenSource.Token).ConfigureAwait(false);

            await this.SetSubscriberStateAsync(SubscriberState.Reachable).ConfigureAwait(false);
            if (catchUpWhenAvailable) await this.CatchUpAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is ObjectDisposedException || ex is TaskCanceledException || ex is OperationCanceledException || (ex is RpcException rpcException && rpcException.StatusCode == StatusCode.Cancelled)) { }
        catch (Exception ex) { await this.OnSubscriptionErrorAsync(ex).ConfigureAwait(false); }
    }

    /// <summary>
    /// Catches up missed <see cref="CloudEvent"/>s
    /// </summary>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    protected virtual async Task CatchUpAsync()
    {
        using var activity = CloudStreamsDefaults.Telemetry.ActivitySource.StartActivity("SubscriptionHandler.CatchUp");
        activity?.SetTag("subscription", this.Subscription.GetQualifiedName());
        var streamInitializationToken = CancellationToken.None;
        var streamInitializationSessionId = 0L;
        TaskCompletionSource? streamSynchronizationCompletionSource = null;
        await this.InitLock.WaitAsync(this.CancellationTokenSource.Token).ConfigureAwait(false);
        try
        {
            if (this.StreamInitializationCancellationTokenSource == null) return;
            streamInitializationToken = this.StreamInitializationCancellationTokenSource.Token;
            streamInitializationSessionId = Interlocked.Read(ref this._streamInitializationSessionId);
            this.StreamSynchronizationTaskCompletionSource ??= new(TaskCreationOptions.RunContinuationsAsynchronously);
            streamSynchronizationCompletionSource = this.StreamSynchronizationTaskCompletionSource;
        }
        finally { this.InitLock.Release(); }
        try
        {
            this.SubscriptionOutOfSync = true;
            var currentOffset = this.Subscription.GetOffset();
            if (currentOffset == StreamPosition.EndOfStream) currentOffset = this.Subscription.Spec.Partition == null ?
                (long)(await this.EventStore.ReadOneAsync(StreamReadDirection.Backwards, StreamPosition.EndOfStream, streamInitializationToken).ConfigureAwait(false))!.Sequence
                : (long)(await this.EventStore.ReadPartitionAsync(this.Subscription.Spec.Partition, StreamReadDirection.Backwards, StreamPosition.EndOfStream, 1, streamInitializationToken).SingleAsync(streamInitializationToken).ConfigureAwait(false))!.Sequence;
            do
            {
                var record = this.Subscription.Spec.Partition == null ?
                    await this.EventStore.ReadOneAsync(StreamReadDirection.Forwards, currentOffset!, streamInitializationToken).ConfigureAwait(false)
                    : await this.EventStore.ReadPartitionAsync(this.Subscription.Spec.Partition, StreamReadDirection.Forwards, currentOffset, 1, streamInitializationToken).SingleOrDefaultAsync(streamInitializationToken).ConfigureAwait(false);
                if (record == null)
                {
                    await Task.Delay(50, streamInitializationToken).ConfigureAwait(false);
                    continue;
                }
                await this.DispatchAsync(record, true, false).ConfigureAwait(false);
                currentOffset++;
            }
            while (!streamInitializationToken.IsCancellationRequested && this.IsStreamInitializationSessionCurrent(streamInitializationSessionId) && (ulong)currentOffset <= this.StreamOffset);
            this.SubscriptionOutOfSync = false;
        }
        catch (Exception ex) when (ex is ObjectDisposedException || ex is TaskCanceledException || ex is OperationCanceledException || (ex is RpcException rpcException && rpcException.StatusCode == StatusCode.Cancelled)) { }
        finally { streamSynchronizationCompletionSource?.TrySetResult(); }
    }

    /// <summary>
    /// Commits the specified offset
    /// </summary>
    /// <param name="offset">The <see cref="Subscription"/>'s offset to commit</param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    protected virtual async Task CommitOffsetAsync(ulong? offset)
    {
        using var activity = CloudStreamsDefaults.Telemetry.ActivitySource.StartActivity("SubscriptionHandler.CommitOffset");
        activity?.SetTag("subscription", this.Subscription.GetQualifiedName());
        activity?.SetTag("offset", offset);
        var resource = this.Subscription.Clone()!;
        if (resource.Status == null) resource.Status = new() { ObservedGeneration = this.Subscription.Metadata.Generation };
        if (resource.Status.Stream == null) resource.Status.Stream = new();
        resource.Status.Stream.AckedOffset = offset;
        resource.Status.ObservedGeneration = this.Subscription.Metadata.Generation;
        var patch = JsonPatchUtility.CreateJsonPatchFromDiff(this.Subscription, resource);
        if (!patch.Operations.Any()) return;
        try
        {
            await this.ResourceRepository.PatchStatusAsync<Subscription>(new Patch(PatchType.JsonPatch, patch), resource.GetName(), resource.GetNamespace(), null, false, this.CancellationTokenSource.Token).ConfigureAwait(false);
        }
        catch (ProblemDetailsException ex) when (ex.Problem.Status == (int)HttpStatusCode.NotModified) { }
    }

    /// <summary>
    /// Cancels the subscription's ongoing synchronization loop, if any
    /// </summary>
    public async Task CancelSynchronizationLoopAsync()
    {
        CancellationTokenSource? streamInitializationCancellationTokenSource = null;
        Task? streamSynchronizationTask = null;
        IDisposable? subscriptionHandle = null;
        await this.InitLock.WaitAsync(this.CancellationTokenSource.Token).ConfigureAwait(false);
        try
        {
            Interlocked.Increment(ref this._streamInitializationSessionId);
            streamInitializationCancellationTokenSource = this.StreamInitializationCancellationTokenSource;
            this.StreamInitializationCancellationTokenSource = null;
            streamSynchronizationTask = this.StreamSynchronizationTaskCompletionSource?.Task;
            this.StreamSynchronizationTaskCompletionSource = null;
            subscriptionHandle = this.SubscriptionHandle;
            this.SubscriptionHandle = null;
        }
        finally { this.InitLock.Release(); }
        streamInitializationCancellationTokenSource?.Cancel();
        if (streamSynchronizationTask != null)
        {
            try
            {
                await streamSynchronizationTask.ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is OperationCanceledException or TaskCanceledException || (ex is RpcException rpcException && rpcException.StatusCode == StatusCode.Cancelled)) { }
        }
        subscriptionHandle?.Dispose();
        streamInitializationCancellationTokenSource?.Dispose();
    }

    /// <summary>
    /// Sets the <see cref="Core.Resources.Subscription"/>'s status phase
    /// </summary>
    /// <param name="phase">The <see cref="Core.Resources.Subscription"/>'s status phase</param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    protected virtual async Task SetStatusPhaseAsync(SubscriptionStatusPhase phase)
    {
        using var activity = CloudStreamsDefaults.Telemetry.ActivitySource.StartActivity("SubscriptionHandler.SetStatusPhase");
        activity?.SetTag("subscription", this.Subscription.GetQualifiedName());
        activity?.SetTag("phase", EnumHelper.GetDisplayName(phase));
        var resource = this.Subscription.Clone()!;
        if (resource.Status == null) resource.Status = new();
        else if (resource.Status.Phase == phase) return;
        resource.Status.Phase = phase;
        var patch = JsonPatchUtility.CreateJsonPatchFromDiff(this.Subscription, resource);
        if (!patch.Operations.Any()) return;
        await this.ResourceRepository.PatchStatusAsync<Subscription>(new Patch(PatchType.JsonPatch, patch), resource.GetName(), resource.GetNamespace(), null, false, this.CancellationTokenSource.Token).ConfigureAwait(false);
    }

    /// <summary>
    /// Sets the subscriber's state
    /// </summary>
    /// <param name="state">The state to set</param>
    /// <param name="reason">The reason why, if any, the subscriber is in the specified state</param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    protected virtual async Task SetSubscriberStateAsync(SubscriberState state, string? reason = null)
    {
        using var activity = CloudStreamsDefaults.Telemetry.ActivitySource.StartActivity("SubscriptionHandler.SetStatusPhase");
        activity?.SetTag("subscription", this.Subscription.GetQualifiedName());
        activity?.SetTag("state", EnumHelper.GetDisplayName(state));
        if(!string.IsNullOrWhiteSpace(reason)) activity?.SetTag("reason", reason);
        if (this.Subscription.Status?.Subscriber?.State == state && this.Subscription.Status?.Subscriber?.Reason == reason) return;
        var resource = this.Subscription.Clone()!;
        resource.Status ??= new();
        resource.Status.Subscriber ??= new();
        resource.Status.Subscriber.State = state;
        resource.Status.Subscriber.Reason = reason;
        var patch = JsonPatchUtility.CreateJsonPatchFromDiff(this.Subscription, resource);
        if (!patch.Operations.Any()) return;
        await this.ResourceRepository.PatchStatusAsync<Subscription>(new Patch(PatchType.JsonPatch, patch), resource.GetName(), resource.GetNamespace(), null, false, this.CancellationTokenSource.Token).ConfigureAwait(false);
    }

    /// <summary>
    /// Handles updates to the managed <see cref="Subscription"/>'s default <see cref="HttpClientRetryPolicy"/>
    /// </summary>
    /// <param name="retryPolicy">The updated default <see cref="HttpClientRetryPolicy"/></param>
    protected virtual void OnDefaultRetryPolicyChanged(HttpClientRetryPolicy retryPolicy)
    {
        this.DefaultRetryPolicy = retryPolicy;
    }

    /// <summary>
    /// Handles changes to the <see cref="Core.Resources.Subscription"/>'s offset
    /// </summary>
    /// <param name="offset">The desired offset</param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    protected virtual async Task OnSubscriptionOffsetChangedAsync(long? offset)
    {
        try
        {
            if (this.Subscription.Status?.Stream?.Fault != null)
            {
                this.LastRequestedOffset = offset;
                var resource = this.Subscription.Clone()!;
                if (resource.Status == null) resource.Status = new() { ObservedGeneration = this.Subscription.Metadata.Generation };
                if (resource.Status.Stream == null) resource.Status.Stream = new();
                resource.Status.Stream.Fault = null;
                var patch = JsonPatchUtility.CreateJsonPatchFromDiff(this.Subscription, resource);
                await this.ResourceRepository.PatchStatusAsync<Subscription>(new Patch(PatchType.JsonPatch, patch), resource.GetName(), resource.GetNamespace(), null, false, this.CancellationTokenSource.Token).ConfigureAwait(false);
                return;
            }
            await this.InitLock.WaitAsync(this.CancellationTokenSource.Token).ConfigureAwait(false);
            try
            {
                if (this.LastRequestedOffset == offset)
                {
                    this.Logger.LogTrace("Ignoring unchanged offset update for subscription '{subscription}' at offset '{offset}'", this.Subscription.GetQualifiedName(), offset);
                    return;
                }
                this.LastRequestedOffset = offset;
            }
            finally { this.InitLock.Release(); }
            await this.CancelSynchronizationLoopAsync().ConfigureAwait(false);
            await this.InitializeCloudEventStreamAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is OperationCanceledException or TaskCanceledException || (ex is RpcException rpcException && rpcException.StatusCode == StatusCode.Cancelled)) { }
        catch (Exception ex) { await this.OnSubscriptionErrorAsync(ex).ConfigureAwait(false); }
    }

    /// <summary>
    /// Handles streaming faults
    /// </summary>
    /// <param name="fault">The <see cref="ProblemDetails"/> that describes the streaming fault to handle</param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    protected virtual async Task OnSubscriptionStreamingFaultAsync(ProblemDetails? fault)
    {
        try
        {
            await this.CancelSynchronizationLoopAsync().ConfigureAwait(false);
            if (fault == null) await this.InitializeCloudEventStreamAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is OperationCanceledException or TaskCanceledException || (ex is RpcException rpcException && rpcException.StatusCode == StatusCode.Cancelled)) { }
        catch (Exception ex) { await this.OnSubscriptionErrorAsync(ex).ConfigureAwait(false); }
    }

    /// <summary>
    /// Handles the specified <see cref="CloudEvent"/>
    /// </summary>
    /// <param name="e">The <see cref="CloudEvent"/> to handle</param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    protected virtual async Task OnCloudEventAsync(CloudEventRecord e)
    {
        try
        {
            this.StreamOffset = e.Sequence;
            if (this.Subscription.Status?.Stream?.Fault != null || this.Subscription?.Status?.Subscriber?.State ==  SubscriberState.Unreachable || this.SubscriptionOutOfSync) return;
            await this.DispatchAsync(e, true, true).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is OperationCanceledException or TaskCanceledException || (ex is RpcException rpcException && rpcException.StatusCode == StatusCode.Cancelled)) { }
        catch (Exception ex) { await this.OnSubscriptionErrorAsync(ex).ConfigureAwait(false); }
    }

    /// <summary>
    /// Handles the specified unhandled <see cref="Exception"/>s that has been thrown during the streaming of filtered <see cref="CloudEvent"/>s
    /// </summary>
    /// <param name="ex">The unhandled <see cref="Exception"/> to handle</param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    protected virtual async Task OnSubscriptionErrorAsync(Exception ex)
    {
        try
        {
            this.StreamInitializationCancellationTokenSource?.Cancel();
            this.Logger.LogError("An error occurred while streaming cloud events for subscription '{subscription}': {ex}", this.Subscription.GetQualifiedName(), ex);
            var resource = this.Subscription.Clone()!;
            if (resource.Spec.Stream == null) return;
            if (resource.Status == null) resource.Status = new() { ObservedGeneration = this.Subscription.Metadata.Generation };
            if (resource.Status.Stream == null) resource.Status.Stream = new();
            resource.Status.Stream.Fault = ex.ToProblemDetails();
            var patch = JsonPatchUtility.CreateJsonPatchFromDiff(this.Subscription, resource);
            await this.ResourceRepository.PatchStatusAsync<Subscription>(new Patch(PatchType.JsonPatch, patch), resource.GetName(), resource.GetNamespace(), null, false, this.CancellationTokenSource.Token).ConfigureAwait(false);
        }
        catch (Exception inner) when (inner is OperationCanceledException or TaskCanceledException || (inner is RpcException rpcException && rpcException.StatusCode == StatusCode.Cancelled)) { }
        
    }

    /// <summary>
    /// Disposes of the <see cref="SubscriptionHandler"/>
    /// </summary>
    /// <param name="disposing">A boolean indicating whether or not the <see cref="SubscriptionHandler"/> is being disposed of</param>
    protected virtual ValueTask DisposeAsync(bool disposing)
    {
        if (!this._disposed)
        {
            if (disposing)
            {
                this.SubscriptionHandle?.Dispose();
                this.CancellationTokenSource.Dispose();
            }
            this._disposed = true;
        }
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await this.DisposeAsync(disposing: true);
        GC.SuppressFinalize(this);
    }

}
