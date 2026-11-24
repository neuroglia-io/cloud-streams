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

    /// <summary>
    /// Gets the default maximum amount of time to wait for a subscriber to respond to a single delivery attempt
    /// </summary>
    public static readonly TimeSpan DefaultDispatchTimeout = TimeSpan.FromSeconds(100);

    readonly object _syncLock = new();
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
        // timeouts are enforced per delivery attempt (see GetDispatchTimeout), so that they can exceed the HttpClient's default of 100 seconds
        this.HttpClient.Timeout = System.Threading.Timeout.InfiniteTimeSpan;
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
    /// Gets the offset last committed by the handler, which may not yet be reflected by <see cref="Subscription"/>'s status
    /// </summary>
    protected ulong? CommittedOffset { get; private set; }

    /// <summary>
    /// Gets the generation of the <see cref="Subscription"/> the <see cref="CommittedOffset"/> was committed for
    /// </summary>
    protected ulong? CommittedOffsetGeneration { get; private set; }

    /// <summary>
    /// Gets the subscriber state last reported by the handler, if any
    /// </summary>
    protected (SubscriberState State, string? Reason)? ReportedSubscriberState { get; private set; }

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
                throw new InvalidOperationException($"Failed to initialize the cloud event stream of subscription '{this.Subscription.GetQualifiedName()}': observed stream is null while initialization token is not canceled (session: {streamInitializationSessionId}, offset: {offset}).");
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
                    // a new stream starts in sync unless it has to catch up: a previous session aborted while out of sync must not wedge this one
                    this.SubscriptionOutOfSync = shouldCatchUp;
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
    /// <returns>A boolean indicating whether or not the <see cref="CloudEventRecord"/> has been handled, which is to say delivered or filtered out. False if its delivery failed for good</returns>
    protected virtual async Task<bool> DispatchAsync(CloudEventRecord e, bool retryOnError, bool catchUpWhenAvailable)
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
        if (!await this.FiltersAsync(e).ConfigureAwait(false)) return true;
        cloudEvent = await this.MutateAsync(cloudEvent).ConfigureAwait(false);
        return await this.DispatchAsync(cloudEvent, e.Sequence, retryOnError, catchUpWhenAvailable).ConfigureAwait(false);
    }

    /// <summary>
    /// Dispatches the specified <see cref="CloudEvent"/> to the configured subscriber
    /// </summary>
    /// <param name="e">The <see cref="CloudEvent"/> to dispatch</param>
    /// <param name="offset">The offset of the <see cref="CloudEvent"/> to dispatch</param>
    /// <param name="retryOnError">A boolean indicating whether or not to retry, according to the configured retry policy, when the delivery fails. If false, delivery failures are thrown as <see cref="HttpRequestException"/>s</param>
    /// <param name="catchUpWhenAvailable">A boolean indicating whether or not to catch up missed events when the subscriber becomes available</param>
    /// <returns>A boolean indicating whether or not the <see cref="CloudEvent"/> has been delivered. False if its delivery failed for good</returns>
    protected virtual async Task<bool> DispatchAsync(CloudEvent e, ulong offset, bool retryOnError, bool catchUpWhenAvailable)
    {
        try
        {
            await this.DeliverAsync(e, offset).ConfigureAwait(false);
            return true;
        }
        catch (HttpRequestException ex) when (retryOnError)
        {
            return await this.RetryDispatchAsync(e, offset, catchUpWhenAvailable, ex).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Performs a single attempt at delivering the specified <see cref="CloudEvent"/> to the configured subscriber and, if successful, acknowledges it
    /// </summary>
    /// <param name="e">The <see cref="CloudEvent"/> to deliver</param>
    /// <param name="offset">The offset of the <see cref="CloudEvent"/> to deliver</param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    /// <exception cref="HttpRequestException">The delivery attempt failed, either because the subscriber could not be reached, did not respond in time or responded with a non-success status code</exception>
    /// <remarks>
    /// <see cref="HttpClient"/> reports a timeout as a <see cref="TaskCanceledException"/>, which is indistinguishable from a shutdown by its type alone.
    /// A cancellation that did not originate from the handler's own <see cref="CancellationTokenSource"/> is therefore a timeout, and is surfaced as an
    /// <see cref="HttpRequestException"/> so that it goes through the retry path like any other delivery failure, rather than being swallowed (see #132)
    /// </remarks>
    protected virtual async Task DeliverAsync(CloudEvent e, ulong offset)
    {
        var timeout = this.GetDispatchTimeout();
        using var timeoutCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(this.CancellationTokenSource.Token);
        timeoutCancellationTokenSource.CancelAfter(timeout);
        try
        {
            using var requestContent = e.ToHttpContent();
            using var request = new HttpRequestMessage(HttpMethod.Post, this.Subscription.Spec.Subscriber.Uri) { Content = requestContent };
            using var response = await this.HttpClient.SendAsync(request, timeoutCancellationTokenSource.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var reason = $"The server responded with a non-success status code '{response.StatusCode}'";
                var responseContent = await response.Content.ReadAsStringAsync(timeoutCancellationTokenSource.Token).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(responseContent)) reason = $"{reason}: {responseContent}";
                throw new HttpRequestException(reason, null, response.StatusCode);
            }
        }
        catch (OperationCanceledException ex) when (!this.CancellationTokenSource.IsCancellationRequested)
        {
            throw new HttpRequestException($"The subscriber did not respond within the configured timeout of {timeout}", ex);
        }
        this.Metrics.IncrementTotalPublishedEvents(this.GetStreamName(), this.Subscription.Spec.Subscriber.Uri.ToString());
        if (this.Subscription.Spec.Stream != null) await this.CommitOffsetAsync(offset + 1).ConfigureAwait(false);
        await this.SetSubscriberStateAsync(SubscriberState.Reachable).ConfigureAwait(false);
        if (this.Subscription.Spec.Subscriber.RateLimit.HasValue) await Task.Delay((int)(1000 / this.Subscription.Spec.Subscriber.RateLimit.Value), this.CancellationTokenSource.Token).ConfigureAwait(false);
    }

    /// <summary>
    /// Retries to dispatch the specified <see cref="CloudEvent"/>, according to the subscription's retry policy or, if not set, to the broker's default one
    /// </summary>
    /// <param name="e">The <see cref="CloudEvent"/> to dispatch</param>
    /// <param name="offset">The offset of the <see cref="CloudEvent"/> to dispatch</param>
    /// <param name="catchUpWhenAvailable">A boolean indicating whether or not to catch up events when the subscribe becomes available again</param>
    /// <param name="initialFailure">The <see cref="HttpRequestException"/> that describes why the initial dispatch attempt has failed</param>
    /// <returns>A boolean indicating whether or not the <see cref="CloudEvent"/> has eventually been delivered</returns>
    /// <remarks>
    /// <para>
    /// Every retry is a single delivery attempt (see <see cref="DeliverAsync(CloudEvent, ulong)"/>) which never re-enters this method, so the attempt and
    /// circuit breaker counters are shared across all attempts, and each failed attempt is counted exactly once (see #131).
    /// </para>
    /// <para>
    /// When the retry policy is exhausted, or when the subscriber responds with a status code the policy is not configured to retry, a subscription that
    /// tracks a stream offset is faulted with a self-explaining reason, so that the event is never skipped silently. A subscription that does not track
    /// an offset drops the event and keeps going, since there is nothing to rewind to.
    /// </para>
    /// </remarks>
    protected virtual async Task<bool> RetryDispatchAsync(CloudEvent e, ulong offset, bool catchUpWhenAvailable, HttpRequestException initialFailure)
    {
        using var activity = CloudStreamsDefaults.Telemetry.ActivitySource.StartActivity("SubscriptionHandler.RetryDispatch");
        activity?.SetTag("subscription", this.Subscription.GetQualifiedName());
        activity?.SetTag("event.id", e.Id);
        activity?.SetTag("event.source", e.Source);
        activity?.SetTag("event.type", e.Type);
        if (!string.IsNullOrWhiteSpace(e.Subject)) activity?.SetTag("event.subject", e.Subject);
        activity?.SetTag("offset", offset);
        activity?.SetTag("catchup", catchUpWhenAvailable);
        activity?.SetTag("reason", initialFailure.Message);
        var streamName = this.GetStreamName();
        var subscriberUri = this.Subscription.Spec.Subscriber.Uri.ToString();
        var policy = this.Subscription.Spec.Subscriber.RetryPolicy ?? this.DefaultRetryPolicy;
        var maxAttempts = policy.MaxAttempts ?? RetryPolicy.DefaultMaxAttempts;
        var backoffDuration = policy.BackoffDuration ?? RetryPolicy.DefaultRetryBackoffDuration;
        var lastFailure = initialFailure;
        var attempts = 0;
        var consecutiveFailures = 1;
        try
        {
            this.SubscriptionOutOfSync = true;
            this.Metrics.IncrementTotalDeliveryFailures(streamName, subscriberUri);
            await this.SetSubscriberStateAsync(SubscriberState.Unreachable, lastFailure.Message).ConfigureAwait(false);
            while (true)
            {
                if (!IsRetryable(policy, lastFailure)) throw new HttpRequestException($"Failed to deliver the cloud event at offset '{offset}' to subscriber '{subscriberUri}': the subscriber responded with status code '{lastFailure.StatusCode}', which the retry policy is not configured to retry. {lastFailure.Message}. {DescribeHowToResume(offset)}", lastFailure, lastFailure.StatusCode);
                if (maxAttempts >= 0 && attempts >= maxAttempts) throw new HttpRequestException($"Failed to deliver the cloud event at offset '{offset}' to subscriber '{subscriberUri}' after {attempts + 1} attempt(s). Last failure: {lastFailure.Message}. {DescribeHowToResume(offset)}", lastFailure, lastFailure.StatusCode);
                attempts++;
                var delay = backoffDuration.ForAttempt(attempts);
                if (policy.CircuitBreaker != null && consecutiveFailures >= policy.CircuitBreaker.BreakAfter)
                {
                    var breakDuration = policy.CircuitBreaker.BreakDuration.ToTimeSpan();
                    this.Logger.LogWarning("The circuit of subscription '{subscription}' has been broken after {failures} consecutive delivery failures. Delivery will resume in {breakDuration}", this.Subscription.GetQualifiedName(), consecutiveFailures, breakDuration);
                    if (breakDuration > delay) delay = breakDuration;
                    consecutiveFailures = 0;
                }
                if (delay > TimeSpan.Zero) await Task.Delay(delay, this.CancellationTokenSource.Token).ConfigureAwait(false);
                try
                {
                    await this.DeliverAsync(e, offset).ConfigureAwait(false);
                    break;
                }
                catch (HttpRequestException ex)
                {
                    lastFailure = ex;
                    consecutiveFailures++;
                    this.Metrics.IncrementTotalDeliveryFailures(streamName, subscriberUri);
                    await this.SetSubscriberStateAsync(SubscriberState.Unreachable, ex.Message).ConfigureAwait(false);
                }
            }
            if (catchUpWhenAvailable) await this.CatchUpAsync().ConfigureAwait(false);
            return true;
        }
        catch (Exception ex) when (ex is ObjectDisposedException || ex is TaskCanceledException || ex is OperationCanceledException || (ex is RpcException rpcException && rpcException.StatusCode == StatusCode.Cancelled))
        {
            return false;
        }
        catch (Exception ex)
        {
            if (this.Subscription.Spec.Stream == null)
            {
                this.Logger.LogWarning("Dropped the cloud event at offset '{offset}' of subscription '{subscription}', which does not track a stream offset: {reason}", offset, this.Subscription.GetQualifiedName(), ex.Message);
                this.SubscriptionOutOfSync = false;
            }
            else await this.OnSubscriptionErrorAsync(ex).ConfigureAwait(false);
            return false;
        }
    }

    /// <summary>
    /// Determines whether or not the specified delivery failure should be retried according to the specified retry policy
    /// </summary>
    /// <param name="policy">The retry policy to use</param>
    /// <param name="failure">The <see cref="HttpRequestException"/> that describes the delivery failure</param>
    /// <returns>A boolean indicating whether or not the specified delivery failure should be retried</returns>
    /// <remarks>Failures that carry no status code (connection failures, timeouts) are always retried, since they say nothing about the event itself</remarks>
    protected static bool IsRetryable(HttpClientRetryPolicy policy, HttpRequestException failure)
    {
        if (!failure.StatusCode.HasValue || policy.StatusCodes == null || policy.StatusCodes.Count == 0) return true;
        return policy.StatusCodes.Contains((int)failure.StatusCode.Value);
    }

    static string DescribeHowToResume(ulong offset) => $"Set the subscription's 'spec.stream.offset' to '{offset}' to retry delivering the event, or to '{offset + 1}' to skip it";

    /// <summary>
    /// Gets the maximum amount of time to wait for the subscriber to respond to a single delivery attempt
    /// </summary>
    /// <returns>The maximum amount of time to wait for the subscriber to respond to a single delivery attempt</returns>
    protected virtual TimeSpan GetDispatchTimeout()
    {
        var timeout = this.Subscription.Spec.Subscriber.Timeout?.ToTimeSpan() ?? this.Broker.Resource.Spec.Dispatch?.Timeout?.ToTimeSpan() ?? DefaultDispatchTimeout;
        return timeout > TimeSpan.Zero ? timeout : DefaultDispatchTimeout;
    }

    /// <summary>
    /// Gets the name of the stream the subscription consumes, used to tag metrics
    /// </summary>
    /// <returns>The name of the stream the subscription consumes</returns>
    protected virtual string GetStreamName() => this.Subscription.Spec.Partition != null ? this.Subscription.Spec.Partition.GetStreamName() : "All";

    /// <summary>
    /// Catches up missed <see cref="CloudEvent"/>s
    /// </summary>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    /// <remarks>
    /// <see cref="StreamOffset"/> is the length of the stream, i.e. the offset of the next record to be appended. Catch-up replays every record up to it,
    /// then leaves the out-of-sync state atomically with respect to <see cref="OnCloudEventAsync(CloudEventRecord)"/>, so that a record appended while
    /// catching up is either replayed here or dispatched live, but never dropped. The out-of-sync state is reset on every exit path, unless another
    /// catch-up has started in the meantime, so an aborted catch-up can never leave the subscription unable to receive anything (see #81 and #132).
    /// </remarks>
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
            if (this.CommittedOffset.HasValue && this.CommittedOffsetGeneration == this.Subscription.Metadata.Generation && (long)this.CommittedOffset.Value > currentOffset) currentOffset = (long)this.CommittedOffset.Value;
            if (currentOffset == StreamPosition.EndOfStream) currentOffset = this.Subscription.Spec.Partition == null ?
                (long)(await this.EventStore.ReadOneAsync(StreamReadDirection.Backwards, StreamPosition.EndOfStream, streamInitializationToken).ConfigureAwait(false))!.Sequence
                : (long)(await this.EventStore.ReadPartitionAsync(this.Subscription.Spec.Partition, StreamReadDirection.Backwards, StreamPosition.EndOfStream, 1, streamInitializationToken).SingleAsync(streamInitializationToken).ConfigureAwait(false))!.Sequence;
            while (true)
            {
                while (!streamInitializationToken.IsCancellationRequested && this.IsStreamInitializationSessionCurrent(streamInitializationSessionId) && (ulong)currentOffset < (this.StreamOffset ?? 0))
                {
                    var record = this.Subscription.Spec.Partition == null ?
                        await this.EventStore.ReadOneAsync(StreamReadDirection.Forwards, currentOffset!, streamInitializationToken).ConfigureAwait(false)
                        : await this.EventStore.ReadPartitionAsync(this.Subscription.Spec.Partition, StreamReadDirection.Forwards, currentOffset, 1, streamInitializationToken).SingleOrDefaultAsync(streamInitializationToken).ConfigureAwait(false);
                    if (record == null)
                    {
                        await Task.Delay(50, streamInitializationToken).ConfigureAwait(false);
                        continue;
                    }
                    if (!await this.DispatchAsync(record, true, false).ConfigureAwait(false)) return;
                    currentOffset++;
                }
                if (streamInitializationToken.IsCancellationRequested || !this.IsStreamInitializationSessionCurrent(streamInitializationSessionId)) return;
                lock (this._syncLock)
                {
                    if ((ulong)currentOffset < (this.StreamOffset ?? 0)) continue;
                    this.SubscriptionOutOfSync = false;
                    return;
                }
            }
        }
        catch (Exception ex) when (ex is ObjectDisposedException || ex is TaskCanceledException || ex is OperationCanceledException || (ex is RpcException rpcException && rpcException.StatusCode == StatusCode.Cancelled)) { }
        finally
        {
            await this.InitLock.WaitAsync(CancellationToken.None).ConfigureAwait(false);
            try
            {
                if (this.StreamSynchronizationTaskCompletionSource == null || this.StreamSynchronizationTaskCompletionSource == streamSynchronizationCompletionSource)
                {
                    this.SubscriptionOutOfSync = false;
                    if (this.StreamSynchronizationTaskCompletionSource == streamSynchronizationCompletionSource) this.StreamSynchronizationTaskCompletionSource = null;
                }
            }
            finally { this.InitLock.Release(); }
            streamSynchronizationCompletionSource?.TrySetResult();
        }
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
        this.CommittedOffset = offset;
        this.CommittedOffsetGeneration = this.Subscription.Metadata.Generation;
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
        // the resource watch lags behind the patches applied by the handler, so the last reported state is tracked locally to avoid patching it after every delivery
        if (this.ReportedSubscriberState == (state, reason)) return;
        this.ReportedSubscriberState = (state, reason);
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
            bool outOfSync;
            lock (this._syncLock)
            {
                // the stream offset is the length of the stream, which is to say the offset of the next record to be appended
                this.StreamOffset = e.Sequence + 1;
                outOfSync = this.SubscriptionOutOfSync;
            }
            // an unreachable subscriber is deliberately not a reason to drop the event: while a delivery is being retried the subscription is out of sync,
            // and a persisted 'unreachable' state (i.e. after a broker restart) would otherwise drop every live event forever
            if (this.Subscription.Status?.Stream?.Fault != null || outOfSync) return;
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
