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
using Json.Patch;
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

    /// <summary>
    /// The maximum length of a subscriber status reason
    /// </summary>
    /// <remarks>
    /// The subscriber's error body is copied into the reason, and an unbounded one - a reverse proxy's HTML error page, say -
    /// is persisted verbatim on a cluster-visible resource, once per failed attempt
    /// </remarks>
    public const int MaxSubscriberReasonLength = 512;

    bool _disposed;
    long _streamInitializationSessionId;
    //set when we write a stream fault, so that the fault -> null transition the watch delivers can be told apart from the
    //first status update of a subscription that was never faulted. Only the former is a resume
    bool _streamFaulted;
    //this.Subscription is refreshed by the resource watch and therefore lags behind a patch we have just written. Without an
    //in-memory record of what we last reported, a healthy subscription re-patches its subscriber state once per event
    //throughout that window
    SubscriberState? _lastReportedSubscriberState;
    string? _lastReportedSubscriberReason;

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
        this._streamFaulted = subscription.Status?.Stream?.Fault != null;
        this.DefaultRetryPolicy = this.Broker.Resource.Spec.Dispatch?.RetryPolicy ?? HttpClientRetryPolicy.Default;
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
    /// Gets the offset of the last <see cref="CloudEventRecord"/> known to exist in the stream, or null when the stream is empty.
    /// <para>
    /// This is an <i>offset</i>, never a length. The distinction is load-bearing: <see cref="CatchUpAsync"/> bounds its loop
    /// with <c>currentOffset &lt;= StreamOffset</c>, so storing a length here makes the loop poll a position that cannot exist
    /// and never reach its own exit.
    /// </para>
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
    /// <remarks>
    /// <para>
    /// Every watch update drops the last-reported subscriber state. That memo absorbs the lag on our <i>own</i> writes only:
    /// once the watch hands us a resource, that resource is the truth, and a memo the store disagrees with would suppress the
    /// re-report that puts the badge back after someone else - an operator, the dashboard, another replica - cleared
    /// <c>status.subscriber</c>.
    /// </para>
    /// <para>
    /// The fault observable does <b>not</b> filter out the fault to null transition. That transition is the only signal an
    /// out-of-band fault clear reaches the handler on, and without it a subscription that spent its delivery attempts stays
    /// deaf for the rest of the process. <see cref="OnSubscriptionStreamingFaultAsync"/> tells a real resume from the first
    /// status update of a subscription that was never faulted.
    /// </para>
    /// </remarks>
    public virtual async Task InitializeAsync(CancellationToken cancellationToken)
    {
        using var activity = CloudStreamsDefaults.Telemetry.ActivitySource.StartActivity("SubscriptionHandler.Initialize");
        this.CancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        this.LastRequestedOffset = this.Subscription.Spec.Stream?.Offset;
        this.SubscriptionController.Where(e => e.Type == ResourceWatchEventType.Updated && e.Resource.GetName() == this.Subscription.GetName() && e.Resource.GetNamespace() == this.Subscription.GetNamespace()).Select(e => e.Resource)
            .Select(subscription =>
            {
                this.Subscription = subscription;
                this._lastReportedSubscriberState = null;
                this._lastReportedSubscriberReason = null;
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
                    .Select(fault => OnSubscriptionStreamingFaultAsync(fault).ToObservable())
            ))
            .Subscribe(this.CancellationTokenSource.Token);
        this.Broker
            .Select(e => e.Resource.Spec.Dispatch?.RetryPolicy)
            .DistinctUntilChanged()
            .Subscribe(policy => this.DefaultRetryPolicy = policy ?? HttpClientRetryPolicy.Default, this.CancellationTokenSource.Token);
        await this.SetStatusPhaseAsync(SubscriptionStatusPhase.Active).ConfigureAwait(false);
        _ = this.InitializeCloudEventStreamAsync();
    }

    /// <summary>
    /// Initializes the <see cref="SubscriptionHandler"/>
    /// </summary>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    /// <remarks>
    /// A successful initialization always leaves <see cref="SubscriptionOutOfSync"/> clear, whether or not it starts a
    /// catch-up: a stream freshly observed from the resolved offset is in sync by definition. Only <see cref="CatchUpAsync"/>'s
    /// <c>finally</c> clears that latch otherwise, so a subscription latched out of sync by a failed delivery would stay
    /// latched - and therefore deaf - whenever the resumed stream turned out to have nothing to replay.
    /// </remarks>
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
                        var length = (await this.EventStore.GetStreamMetadataAsync(streamInitializationToken).ConfigureAwait(false)).Length;
                        this.StreamOffset = length == 0 ? null : length - 1;
                        //'offset' is the next position to read, so anything at or past the length has nothing left to replay
                        if (offset >= 0 && (ulong)offset >= length) offset = StreamPosition.EndOfStream;
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
                        var length = (await this.EventStore.GetPartitionMetadataAsync(this.Subscription.Spec.Partition, streamInitializationToken).ConfigureAwait(false)).Length;
                        this.StreamOffset = length == 0 ? null : length - 1;
                        //'offset' is the next position to read, so anything at or past the length has nothing left to replay
                        if (offset >= 0 && (ulong)offset >= length) offset = StreamPosition.EndOfStream;
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
                    //'offset' is the next position to read, so equality still leaves one record to fetch
                    shouldCatchUp = this.Subscription.Status?.ObservedGeneration == null || (offset != StreamPosition.EndOfStream && (ulong)offset <= this.StreamOffset);
                    initializationOutcome = StreamInitializationOutcomes.Initialized;
                    subscriptionHandle = null;
                }
            }
            finally { this.InitLock.Release(); }
            subscriptionHandle?.Dispose();
            if (initializationOutcome == StreamInitializationOutcomes.Initialized)
            {
                if (shouldCatchUp) _ = this.CatchUpAsync().ConfigureAwait(false);
                else this.SubscriptionOutOfSync = false;
            }
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
    /// <remarks>
    /// The retry policy owns every attempt, including the first: <c>maxAttempts</c> counts total delivery attempts for an
    /// event, so sending once here and then handing off to the policy would add an extra, un-delayed request per event.
    /// </remarks>
    protected virtual async Task DispatchAsync(CloudEvent e, ulong offset, bool retryOnError, bool catchUpWhenAvailable)
    {
        if (retryOnError) await this.RetryDispatchAsync(e, offset, catchUpWhenAvailable).ConfigureAwait(false);
        else
        {
            await this.SendAsync(e, offset).ConfigureAwait(false);
            await this.OnDeliverySucceededAsync(offset).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Performs a single delivery attempt of the specified <see cref="CloudEvent"/> to the configured subscriber
    /// </summary>
    /// <param name="e">The <see cref="CloudEvent"/> to deliver</param>
    /// <param name="offset">The offset of the <see cref="CloudEvent"/> to deliver</param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    /// <exception cref="HttpRequestException">Thrown when the subscriber responds with a non-success status code, or when the request fails at the transport level</exception>
    /// <remarks>
    /// <para>
    /// Failures are thrown, never handled here. Handling them one frame below the delegate the retry policy executes is exactly
    /// what silently disabled every retry policy in <see href="https://github.com/neuroglia-io/cloud-streams/issues/131">#131</see>.
    /// </para>
    /// <para>
    /// The request is all this does. Acknowledgement and status reporting live in <see cref="OnDeliverySucceededAsync"/>, one
    /// frame outside the retried unit: the resource repository is itself remote, so a control-plane failure raised after a
    /// successful POST is an <see cref="HttpRequestException"/> too - and inside the delegate it would be indistinguishable
    /// from a subscriber failure, counted as one, and answered by POSTing the already-delivered event again.
    /// </para>
    /// <para>
    /// A non-success response is thrown rather than passed to <c>EnsureSuccessStatusCode()</c>, so that the response body
    /// survives into the status reason while the exception still carries the status code the retry predicate reads. The body is
    /// truncated to <see cref="MaxSubscriberReasonLength"/> here, at the source, so neither that reason nor the problem details
    /// built from this exception can carry an arbitrarily large subscriber error page onto a cluster-visible resource.
    /// </para>
    /// </remarks>
    protected virtual async Task SendAsync(CloudEvent e, ulong offset)
    {
        using var requestContent = e.ToHttpContent();
        using var request = new HttpRequestMessage(HttpMethod.Post, this.Subscription.Spec.Subscriber.Uri) { Content = requestContent };
        using var response = await this.HttpClient.SendAsync(request, this.CancellationTokenSource.Token).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var reason = $"The server responded with a non-success status code '{response.StatusCode}'";
            var responseContent = await response.Content.ReadAsStringAsync(this.CancellationTokenSource.Token).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(responseContent)) reason = $"{reason}: {Truncate(responseContent, MaxSubscriberReasonLength)}";
            throw new HttpRequestException(reason, null, response.StatusCode);
        }
    }

    /// <summary>
    /// Acknowledges a delivery the subscriber has accepted
    /// </summary>
    /// <param name="offset">The offset of the delivered <see cref="CloudEvent"/></param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    /// <remarks>
    /// Deliberately outside the unit the retry policy executes. See <see cref="SendAsync"/>.
    /// </remarks>
    protected virtual async Task OnDeliverySucceededAsync(ulong offset)
    {
        var streamName = this.Subscription.Spec.Partition != null ? this.Subscription.Spec.Partition.GetStreamName() : "All";
        var subscriberUri = this.Subscription.Spec.Subscriber.Uri.ToString();
        this.Metrics.IncrementTotalPublishedEvents(streamName, subscriberUri);
        await this.SetSubscriberStateAsync(SubscriberState.Reachable).ConfigureAwait(false);
        if (this.Subscription.Spec.Stream != null) await this.CommitOffsetAsync(offset + 1).ConfigureAwait(false);
        if (this.Subscription.Spec.Subscriber.RateLimit.HasValue) await Task.Delay((int)(1000 / this.Subscription.Spec.Subscriber.RateLimit.Value), this.CancellationTokenSource.Token).ConfigureAwait(false);
    }

    /// <summary>
    /// Truncates the specified text to the specified maximum length
    /// </summary>
    /// <param name="text">The text to truncate</param>
    /// <param name="maxLength">The maximum length of the returned text, ellipsis included</param>
    /// <returns>The truncated text</returns>
    static string Truncate(string text, int maxLength) => text.Length <= maxLength ? text : $"{text[..(maxLength - 3)]}...";

    /// <summary>
    /// Determines whether or not the specified <see cref="Exception"/> is a failure to deliver to the subscriber
    /// </summary>
    /// <param name="ex">The <see cref="Exception"/> to check</param>
    /// <returns>A boolean indicating whether or not the specified <see cref="Exception"/> is a delivery failure</returns>
    /// <remarks>
    /// An <see cref="HttpClient"/> timeout raises a <see cref="TaskCanceledException"/>, not an <see cref="HttpRequestException"/>.
    /// The handler's own token tells the two apart: on shutdown it is canceled, on a timeout it is not.
    /// </remarks>
    protected virtual bool IsDeliveryFailure(Exception ex) => ex is HttpRequestException || (ex is TaskCanceledException && !this.CancellationTokenSource.IsCancellationRequested);

    /// <summary>
    /// Dispatches the specified <see cref="CloudEvent"/> under the effective <see cref="HttpClientRetryPolicy"/>, which owns
    /// every delivery attempt for the event, including the first
    /// </summary>
    /// <param name="e">The <see cref="CloudEvent"/> to dispatch</param>
    /// <param name="offset">The offset of the <see cref="CloudEvent"/> to dispatch</param>
    /// <param name="catchUpWhenAvailable">A boolean indicating whether or not to catch up events when the subscribe becomes available again</param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    /// <remarks>
    /// <para>
    /// When the attempts are spent the delivery fails and the stream is faulted. The offset is <b>not</b> acknowledged, so the
    /// event stays in the store and is replayed once an operator clears the fault: re-initialization runs a catch-up, which
    /// dispatches directly rather than through the live path, and a success there restores the subscriber to reachable.
    /// </para>
    /// <para>
    /// The trailing catch-up is gated on this delivery having actually failed first. Because the policy owns the first attempt,
    /// this method is the normal path for <i>every</i> live event, so an ungated catch-up would run after every healthy
    /// dispatch: for a subscription tracking no offset it re-reads and re-delivers the event just sent, and for one that does it
    /// costs a store read and a 50ms poll per event with the live path latched out of sync throughout.
    /// </para>
    /// <para>
    /// Both a failed attempt and an exhausted policy report the subscriber unreachable. Reporting only from Polly's retry
    /// callback is not enough: it never fires for the attempt that exhausts the policy, and with <c>maxAttempts: 1</c> - a
    /// legitimate deliver-once configuration - it never fires at all, leaving the badge on <c>unknown</c> or a stale
    /// <c>reachable</c> for exactly the deliveries that failed outright.
    /// </para>
    /// <para>
    /// A failed delivery also means live events may have been missed while retrying, so <see cref="SubscriptionOutOfSync"/>
    /// short-circuits the live path until a catch-up has run. That latch is cleared by <see cref="CatchUpAsync"/>'s
    /// <c>finally</c>, or by a re-initialization that finds nothing to replay: on recovery this method calls the former
    /// directly, on failure it is reached through the fault an operator clears.
    /// </para>
    /// <para>
    /// The circuit breaker is rebuilt for every event, so an open circuit is not transient state to wait out: it means this
    /// event's attempts are spent, which is the same outcome as exhausting <c>maxAttempts</c> by a different route. It is
    /// therefore faulted like one, so an operator has the same lever to resume with - reporting only the subscriber state would
    /// leave nothing to clear, and <see cref="SubscriberState.Unreachable"/> alone blocks the live path permanently.
    /// </para>
    /// <para>
    /// A transport failure carries no status code, so a policy declaring <c>statusCodes</c> would otherwise stop retrying
    /// exactly the failures retrying is most likely to resolve. A timeout has none either, and is routed through the type
    /// predicate instead, which deliberately bypasses <c>statusCodes</c> - a timeout is always retryable.
    /// </para>
    /// </remarks>
    protected virtual async Task RetryDispatchAsync(CloudEvent e, ulong offset, bool catchUpWhenAvailable)
    {
        using var activity = CloudStreamsDefaults.Telemetry.ActivitySource.StartActivity("SubscriptionHandler.RetryDispatch");
        activity?.SetTag("subscription", this.Subscription.GetQualifiedName());
        activity?.SetTag("event.id", e.Id);
        activity?.SetTag("event.source", e.Source);
        activity?.SetTag("event.type", e.Type);
        if (!string.IsNullOrWhiteSpace(e.Subject)) activity?.SetTag("event.subject", e.Subject);
        activity?.SetTag("offset", offset);
        activity?.SetTag("catchup", catchUpWhenAvailable);
        var streamName = this.Subscription.Spec.Partition != null ? this.Subscription.Spec.Partition.GetStreamName() : "All";
        var subscriberUri = this.Subscription.Spec.Subscriber.Uri.ToString();
        try
        {
            var policyConfiguration = this.Subscription.Spec.Subscriber.RetryPolicy ?? this.DefaultRetryPolicy;
            bool exceptionPredicate(HttpRequestException ex) => policyConfiguration.StatusCodes == null || policyConfiguration.StatusCodes.Count == 0 || !ex.StatusCode.HasValue || policyConfiguration.StatusCodes.Contains((int)ex.StatusCode.Value);
            var policyBuilder = Policy.Handle((Func<HttpRequestException, bool>)exceptionPredicate).Or<TaskCanceledException>(_ => !this.CancellationTokenSource.IsCancellationRequested);
            TimeSpan backoffFor(int attempt) => policyConfiguration.BackoffDuration == null ? TimeSpan.FromSeconds(3) : policyConfiguration.BackoffDuration.ForAttempt(attempt);
            var deliveryRecovered = false;
            async Task onRetryAsync(Exception ex)
            {
                this.SubscriptionOutOfSync = true;
                deliveryRecovered = true;
                await this.SetSubscriberStateAsync(SubscriberState.Unreachable, this.DescribeDeliveryFailure(ex)).ConfigureAwait(false);
            }
            //explicitly typed so that overload resolution cannot bind these to the fire-and-forget Action overloads. Mind the
            //argument order, which differs between the two: WaitAndRetryAsync yields (exception, delay, attempt, context),
            //WaitAndRetryForeverAsync yields (exception, attempt, delay)
            Func<Exception, TimeSpan, int, Context, Task> onRetryBounded = (ex, _, _, _) => onRetryAsync(ex);
            Func<Exception, int, TimeSpan, Task> onRetryForever = (ex, _, _) => onRetryAsync(ex);

            AsyncCircuitBreakerPolicy? circuitBreakerPolicy = policyConfiguration.CircuitBreaker == null ? null
                : policyBuilder.CircuitBreakerAsync(policyConfiguration.CircuitBreaker.BreakAfter, policyConfiguration.CircuitBreaker.BreakDuration.ToTimeSpan());

            //'maxAttempts' counts the first delivery attempt as well as the retries, hence the -1
            AsyncPolicy retryPolicy = policyConfiguration.MaxAttempts.HasValue
                ? policyBuilder.WaitAndRetryAsync(Math.Max(0, policyConfiguration.MaxAttempts.Value - 1), backoffFor, onRetryBounded)
                : policyBuilder.WaitAndRetryForeverAsync(backoffFor, onRetryForever);

            retryPolicy = circuitBreakerPolicy == null ? retryPolicy : retryPolicy.WrapAsync(circuitBreakerPolicy);
            await retryPolicy.ExecuteAsync(async _ =>
            {
                try
                {
                    await this.SendAsync(e, offset).ConfigureAwait(false);
                }
                catch (Exception ex) when (this.IsDeliveryFailure(ex))
                {
                    //counted here rather than in onRetryAsync, which never fires for the attempt that exhausts the policy, and
                    //never fired once per real request before this - it fired once per recursion level
                    this.Metrics.IncrementTotalDeliveryFailures(streamName, subscriberUri);
                    throw;
                }
            }, this.CancellationTokenSource.Token).ConfigureAwait(false);
            await this.OnDeliverySucceededAsync(offset).ConfigureAwait(false);
            if (catchUpWhenAvailable && deliveryRecovered) await this.CatchUpAsync().ConfigureAwait(false);
        }
        catch (BrokenCircuitException ex)
        {
            await this.SetSubscriberStateAsync(SubscriberState.Unreachable, $"The circuit to the subscriber is open: {ex.Message}").ConfigureAwait(false);
            await this.OnSubscriptionErrorAsync(ex).ConfigureAwait(false);
        }
        //narrowed to actual shutdown: a delivery that exhausts its attempts on a timeout also raises a TaskCanceledException, and
        //swallowing that here would reinstate the silent skip reported as #132
        catch (Exception ex) when (ex is ObjectDisposedException || ((ex is TaskCanceledException or OperationCanceledException) && this.CancellationTokenSource.IsCancellationRequested) || (ex is RpcException rpcException && rpcException.StatusCode == StatusCode.Cancelled)) { }
        catch (Exception ex)
        {
            if (this.IsDeliveryFailure(ex))
            {
                this.SubscriptionOutOfSync = true;
                await this.SetSubscriberStateAsync(SubscriberState.Unreachable, this.DescribeDeliveryFailure(ex)).ConfigureAwait(false);
            }
            await this.OnSubscriptionErrorAsync(ex).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Describes why a delivery attempt failed, for use as a subscriber status reason
    /// </summary>
    /// <param name="ex">The <see cref="Exception"/> the delivery attempt failed with</param>
    /// <returns>A string that describes the failure</returns>
    protected virtual string DescribeDeliveryFailure(Exception ex) => ex switch
    {
        HttpRequestException httpRequestException => httpRequestException.InnerException == null ? httpRequestException.Message : httpRequestException.InnerException.Message,
        TaskCanceledException taskCanceledException => taskCanceledException.InnerException is TimeoutException
            ? $"The request to the subscriber timed out after '{this.HttpClient.Timeout}'"
            : "The request to the subscriber was canceled before completion",
        _ => ex.Message
    };

    /// <summary>
    /// Catches up missed <see cref="CloudEvent"/>s
    /// </summary>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    /// <remarks>
    /// <para>
    /// A completed <see cref="StreamSynchronizationTaskCompletionSource"/> is replaced, never reused. It is the handle both
    /// <see cref="CancelSynchronizationLoopAsync"/> and <see cref="InitializeCloudEventStreamAsync"/> drain an in-flight
    /// catch-up through, and awaiting one a <i>previous</i> catch-up already completed returns instantly - disposing the
    /// subscription handle and the initialization token source out from under a loop still reading and dispatching.
    /// </para>
    /// <para>
    /// <see cref="SubscriptionOutOfSync"/> is cleared in the <c>finally</c> rather than on the loop's success line, so that an
    /// early exit cannot leave the subscription latched into dropping every live event - but only by the session that still owns
    /// the stream. A superseded catch-up clearing it would un-gate the live path while its successor is still mid-backlog,
    /// interleaving live dispatches with the replay. Whoever superseded it clears the latch in turn, which
    /// <see cref="InitializeCloudEventStreamAsync"/> does on both of its outcomes.
    /// </para>
    /// <para>
    /// The general <c>catch</c> exists because a non-cancellation exception would otherwise escape a fire-and-forget task
    /// unobserved: no log, no fault, no fix. <see cref="OnSubscriptionErrorAsync"/> is itself total - it has to be, or this
    /// catch would reinstate the very thing it prevents.
    /// </para>
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
            if (this.StreamSynchronizationTaskCompletionSource == null || this.StreamSynchronizationTaskCompletionSource.Task.IsCompleted) this.StreamSynchronizationTaskCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
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
        }
        catch (Exception ex) when (ex is ObjectDisposedException || ex is TaskCanceledException || ex is OperationCanceledException || (ex is RpcException rpcException && rpcException.StatusCode == StatusCode.Cancelled)) { }
        catch (Exception ex) { await this.OnSubscriptionErrorAsync(ex).ConfigureAwait(false); }
        finally
        {
            if (this.IsStreamInitializationSessionCurrent(streamInitializationSessionId)) this.SubscriptionOutOfSync = false;
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
        var patch = JsonPatchUtility.CreateJsonPatchFromDiff(this.Subscription, resource);
        try
        {
            await this.PatchSubscriptionStatusAsync(resource, patch).ConfigureAwait(false);
        }
        catch (ProblemDetailsException ex) when (ex.Problem.Status == (int)HttpStatusCode.NotModified) { }
    }

    /// <summary>
    /// Applies the specified <see cref="JsonPatch"/> to the managed <see cref="Core.Resources.Subscription"/>'s status
    /// </summary>
    /// <param name="resource">The <see cref="Core.Resources.Subscription"/> the patch was built against</param>
    /// <param name="patch">The <see cref="JsonPatch"/> to apply</param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    /// <remarks>
    /// The patched resource is adopted as the handler's view rather than discarded. Every status write here is a diff against
    /// <c>this.Subscription</c>, which is refreshed by the resource watch and therefore lags behind our own writes: diffing
    /// against a stale snapshot yields a whole-object <c>add /status</c> that silently erases the sibling properties an earlier
    /// write had just set - reporting a subscriber unreachable and then wiping that state out when the stream faults.
    /// </remarks>
    protected virtual async Task PatchSubscriptionStatusAsync(Subscription resource, JsonPatch patch)
    {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(patch);
        if (!patch.Operations.Any()) return;
        var patched = await this.ResourceRepository.PatchStatusAsync<Subscription>(new Patch(PatchType.JsonPatch, patch), resource.GetName(), resource.GetNamespace(), null, false, this.CancellationTokenSource.Token).ConfigureAwait(false);
        if (patched != null) this.Subscription = patched;
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
        await this.PatchSubscriptionStatusAsync(resource, patch).ConfigureAwait(false);
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
        //checked before the resource, which lags behind our own writes until the watch delivers them back
        if (this._lastReportedSubscriberState == state && this._lastReportedSubscriberReason == reason) return;
        if (this.Subscription.Status?.Subscriber?.State == state && this.Subscription.Status?.Subscriber?.Reason == reason)
        {
            this._lastReportedSubscriberState = state;
            this._lastReportedSubscriberReason = reason;
            return;
        }
        var resource = this.Subscription.Clone()!;
        resource.Status ??= new();
        resource.Status.Subscriber ??= new();
        resource.Status.Subscriber.State = state;
        resource.Status.Subscriber.Reason = reason;
        var patch = JsonPatchUtility.CreateJsonPatchFromDiff(this.Subscription, resource);
        await this.PatchSubscriptionStatusAsync(resource, patch).ConfigureAwait(false);
        this._lastReportedSubscriberState = state;
        this._lastReportedSubscriberReason = reason;
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
    /// <remarks>
    /// Editing the offset of a faulted subscription is the documented way to resume it, so this clears the fault <b>and</b>
    /// re-initializes. Clearing and returning is only half a resume: nothing else restarts the stream, and the edited value has
    /// already been consumed by <c>DistinctUntilChanged</c>, so an operator who edits it once and sees nothing happen has no
    /// second lever. The in-memory fault flag is dropped before the patch, so the fault to null watch update this write produces
    /// is recognised as our own and does not enter the resume a second time through
    /// <see cref="OnSubscriptionStreamingFaultAsync"/>.
    /// </remarks>
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
                this._streamFaulted = false;
                await this.PatchSubscriptionStatusAsync(resource, patch).ConfigureAwait(false);
                await this.CancelSynchronizationLoopAsync().ConfigureAwait(false);
                await this.InitializeCloudEventStreamAsync().ConfigureAwait(false);
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
    /// <remarks>
    /// A null fault only means a resume when one was actually standing. The observable this is fed from emits whatever the
    /// fault field holds, not only faults, so the first status update of a subscription that was never faulted also carries a
    /// null - and tearing the stream down over that would restart every subscription on its first offset commit.
    /// </remarks>
    protected virtual async Task OnSubscriptionStreamingFaultAsync(ProblemDetails? fault)
    {
        try
        {
            if (fault == null && !this._streamFaulted) return;
            this._streamFaulted = fault != null;
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
    /// <remarks>
    /// <para>
    /// The fault is recorded for every subscription, not just those tracking an offset: a subscription that gives up without
    /// leaving one behind is deaf with nothing to show for it, which is the silence reported by
    /// <see href="https://github.com/neuroglia-io/cloud-streams/issues/28">#28</see>,
    /// <see href="https://github.com/neuroglia-io/cloud-streams/issues/105">#105</see> and
    /// <see href="https://github.com/neuroglia-io/cloud-streams/issues/132">#132</see>.
    /// </para>
    /// <para>
    /// This method is <b>total</b>: reporting a fault must never itself throw. Every call site is already handling an error,
    /// several of them from inside a <c>catch</c> on a fire-and-forget task, where an exception raised here would escape
    /// unobserved - which is the silence it exists to break.
    /// </para>
    /// </remarks>
    protected virtual async Task OnSubscriptionErrorAsync(Exception ex)
    {
        try
        {
            this.StreamInitializationCancellationTokenSource?.Cancel();
            this.Logger.LogError("An error occurred while streaming cloud events for subscription '{subscription}': {ex}", this.Subscription.GetQualifiedName(), ex);
            var resource = this.Subscription.Clone()!;
            if (resource.Status == null) resource.Status = new() { ObservedGeneration = this.Subscription.Metadata.Generation };
            if (resource.Status.Stream == null) resource.Status.Stream = new();
            resource.Status.Stream.Fault = ex.ToProblemDetails();
            var patch = JsonPatchUtility.CreateJsonPatchFromDiff(this.Subscription, resource);
            this._streamFaulted = true;
            await this.PatchSubscriptionStatusAsync(resource, patch).ConfigureAwait(false);
        }
        catch (Exception inner) when (inner is OperationCanceledException or TaskCanceledException || (inner is RpcException rpcException && rpcException.StatusCode == StatusCode.Cancelled)) { }
        catch (Exception inner) { this.Logger.LogError("Failed to report the streaming fault of subscription '{subscription}': {ex}", this.Subscription.GetQualifiedName(), inner); }
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
