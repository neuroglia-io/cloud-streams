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
using Json.Patch;
using Json.Pointer;
using System.Buffers.Text;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Grpc.Core;
using System.Numerics;
using System.Reactive.Threading.Tasks;
using System.Text.RegularExpressions;
using CloudStreams.Broker.Application.Configuration;
using Microsoft.Extensions.Options;
using CloudStreams.Broker.Application.Services.Delivery;

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

    /// <summary>
    /// Gets the maximum number of attempts at recording a streaming fault in the subscription's status
    /// </summary>
    const int MaxFaultReportingAttempts = 5;

    readonly object _syncLock = new();
    IDisposable? _lagRegistration;
    readonly SemaphoreSlim _commitLock = new(1, 1);
    ulong? _uncommittedOffset;
    ulong? _uncommittedOffsetGeneration;
    int _uncommittedDeliveries;

    CancellationTokenRegistration _applicationStoppingRegistration;
    long _nextDeliveryTimestamp;
    // released when live records tell a subscription delivering in batches or in parallel that there is more to read (see #212)
    readonly SemaphoreSlim _pumpSignal = new(0, 1);
    ulong? _nextOffset;
    // the offset to start at, instead of the end of the stream, when the stream did not exist yet when the subscription started observing it
    ulong? _startOffset;
    // the cloud event last sent to the subscriber, as mutated, which is the one that is dead-lettered if its delivery fails for good (see #207)
    (ulong Offset, CloudEvent Event)? _lastAttempt;
    volatile bool _disposed;
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
    /// <param name="deadLetters">The service used to store the cloud events the subscription fails to deliver</param>
    /// <param name="options">The service used to access the current <see cref="BrokerOptions"/></param>
    /// <param name="subscription">The <see cref="Core.Resources.Subscription"/> to dispatch <see cref="CloudEvent"/>s to</param>
    public SubscriptionHandler(ILoggerFactory loggerFactory, IHostApplicationLifetime hostApplicationLifetime, IJsonSerializer serializer, ICloudEventStore cloudEventStore, IResourceRepository resourceRepository, IResourceController<Subscription> subscriptionController,
        IResourceMonitor<Core.Resources.Broker> broker, IExpressionEvaluator expressionEvaluator, IEnumerable<IValidator<CloudEvent>> cloudEventValidators, HttpClient httpClient, IBrokerMetrics metrics, IDeadLetterStore deadLetters, IOptions<BrokerOptions> options, Subscription subscription)
    {
        this.DeadLetters = deadLetters;
        this.Pipeline = new(expressionEvaluator, cloudEventValidators, httpClient, serializer);
        this.Client = new(httpClient);
        this.Acknowledgements = new(delta => this.Metrics.AddPendingAcknowledgements(this.Subscription.GetQualifiedName(), this.GetStreamName(), delta), this.OnAcknowledgementTimeoutAsync);
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
        this.Options = options.Value;
        this.Subscription = subscription;
        this.StatusReporter = new(resourceRepository, this.Logger, subscription);
        this.DefaultRetryPolicy = this.Broker.Resource.Spec.Dispatch?.RetryPolicy ?? new HttpClientRetryPolicy();
        this._applicationStoppingRegistration = hostApplicationLifetime.ApplicationStopping.Register(this.OnApplicationStopping);
    }

    /// <summary>
    /// Handles the application's shutdown, by reporting the subscription as inactive
    /// </summary>
    protected virtual async void OnApplicationStopping()
    {
        if (this._disposed) return;
        try { await this.SetStatusPhaseAsync(SubscriptionStatusPhase.Inactive).ConfigureAwait(false); }
        catch (Exception ex) { this.Logger.LogWarning(ex, "Failed to report subscription '{subscription}' as inactive", this.Subscription.GetQualifiedName()); }
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
    /// Gets the service used to store the cloud events the subscription fails to deliver
    /// </summary>
    protected IDeadLetterStore DeadLetters { get; }

    /// <summary>
    /// Gets the pipeline that filters, mutates and validates the records the subscription consumes
    /// </summary>
    protected SubscriptionEventPipeline Pipeline { get; }

    /// <summary>
    /// Gets the service used to post cloud events to the subscriber and to the dead-letter service
    /// </summary>
    protected SubscriberClient Client { get; }

    /// <summary>
    /// Gets the service used to report the subscription's status
    /// </summary>
    protected SubscriptionStatusReporter StatusReporter { get; }

    /// <summary>
    /// Gets the service used to track the acknowledgement the subscriber has deferred, if any
    /// </summary>
    protected AcknowledgementCoordinator Acknowledgements { get; }

    /// <summary>
    /// Gets the current <see cref="BrokerOptions"/>
    /// </summary>
    protected BrokerOptions Options { get; }

    /// <summary>
    /// Gets the <see cref="Core.Resources.Subscription"/> to dispatch <see cref="CloudEvent"/>s to
    /// </summary>
    protected Subscription Subscription { get; private set; }

    /// <summary>
    /// Gets the default <see cref="HttpClientRetryPolicy"/>
    /// </summary>
    protected HttpClientRetryPolicy DefaultRetryPolicy { get; private set; }

    /// <summary>
    /// Gets the length of the stream, which is to say the offset of the next record to be appended, as last observed by the handler
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
    protected (SubscriberState State, string? Reason)? ReportedSubscriberState => this.StatusReporter.ReportedSubscriberState;

    /// <summary>
    /// Gets a boolean indicating whether or not delivery is suspended until the subscriber acknowledges a cloud event whose acknowledgement it has deferred
    /// </summary>
    protected bool AwaitingAcknowledgement => this.Acknowledgements.IsAwaiting;

    /// <summary>
    /// Gets the token of the pending acknowledgement, if any
    /// </summary>
    protected string? PendingAcknowledgementToken => this.Acknowledgements.Token;

    /// <summary>
    /// Gets the offset of the cloud event whose acknowledgement is pending, if any
    /// </summary>
    protected ulong PendingAcknowledgementOffset => this.Acknowledgements.Offset;

    /// <summary>
    /// Initializes the <see cref="SubscriptionHandler"/>
    /// </summary>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    public virtual async Task InitializeAsync(CancellationToken cancellationToken)
    {
        using var activity = CloudStreamsDefaults.Telemetry.ActivitySource.StartActivity("SubscriptionHandler.Initialize");
        this.CancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        this.LastRequestedOffset = this.Subscription.Spec.Stream?.Offset;
        this._lagRegistration ??= this.Metrics.ObserveLag(this.Subscription.GetQualifiedName(), this.GetLag);
        this.SubscriptionController.Where(e => e.Type == ResourceWatchEventType.Updated && e.Resource.GetName() == this.Subscription.GetName() && e.Resource.GetNamespace() == this.Subscription.GetNamespace()).Select(e => e.Resource)
            .Select(subscription =>
            {
                this.Subscription = subscription;
                this.StatusReporter.Latest = subscription;
                return subscription;
            })
            .Publish(shared => Observable.Merge(
                shared.Where(subscription => subscription.Spec.Stream != null)
                    .Select(subscription => subscription.Spec.Stream!.Offset)
                    .DistinctUntilChanged()
                    .Select(offset => OnSubscriptionOffsetChangedAsync(offset).ToObservable()),
                // the handler reacts to the subscription becoming faulted, which stops streaming, and to its fault being cleared, which resumes it: a fault is
                // cleared when the offset of a faulted subscription is changed, and was otherwise never followed by streaming again (see #204)
                shared.Select(s => s.Status?.Stream?.Fault)
                    .StartWith(this.Subscription.Status?.Stream?.Fault)
                    .DistinctUntilChanged(fault => fault != null)
                    .Skip(1)
                    .Select(fault => OnSubscriptionStreamingFaultAsync(fault).ToObservable()),
                shared.Select(s => s.Spec.Suspended)
                    .StartWith(this.Subscription.Spec.Suspended)
                    .DistinctUntilChanged()
                    .Skip(1)
                    .Select(suspended => OnSubscriptionSuspensionChangedAsync(suspended).ToObservable()),
                // delivering in batches or in parallel, rather than one live record at a time, requires observing the stream again (see #212)
                shared.Select(s => s.Spec.Subscriber.Batch != null || s.Spec.Subscriber.Ordering != null)
                    .StartWith(this.IsPumped)
                    .DistinctUntilChanged()
                    .Skip(1)
                    .Select(_ => OnDeliveryModeChangedAsync().ToObservable()),
                shared.Select(s => s.Status?.Subscriber?.PendingAck)
                    .DistinctUntilChanged(pendingAck => pendingAck?.Token)
                    .Select(pendingAck => OnPendingAcknowledgementChangedAsync(pendingAck).ToObservable())
            ))
            .Subscribe(this.CancellationTokenSource.Token);
        this.Broker
            .Select(e => e.Resource.Spec.Dispatch?.RetryPolicy)
            .DistinctUntilChanged()
            .Subscribe(policy => this.DefaultRetryPolicy = policy ?? new HttpClientRetryPolicy(), this.CancellationTokenSource.Token);
        if (this.Subscription.Spec.Suspended)
        {
            // the offset may have been changed while the broker was down: the change is applied now, since resuming takes the acknowledged offset as current (see #214)
            if (this.IsOffsetResetRequested()) await this.ResetOffsetAsync().ConfigureAwait(false);
            await this.SetStatusPhaseAsync(SubscriptionStatusPhase.Suspended).ConfigureAwait(false);
            return;
        }
        await this.SetStatusPhaseAsync(SubscriptionStatusPhase.Active).ConfigureAwait(false);
        _ = this.InitializeCloudEventStreamAsync();
    }

    /// <summary>
    /// Determines whether or not the subscription's offset has been changed by a user since the broker last committed it
    /// </summary>
    /// <returns>A boolean indicating whether or not the subscription's offset has been changed since the broker last committed it</returns>
    protected virtual bool IsOffsetResetRequested() => this.Subscription.Metadata.Generation > this.Subscription.Status?.ObservedGeneration
        && this.Subscription.Spec.Stream?.Offset != (long?)this.Subscription.Status?.Stream?.AckedOffset;

    /// <summary>
    /// Discards the subscription's acknowledged offset, and whatever acknowledgement is pending, so that delivery restarts at the offset requested by its spec
    /// </summary>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    protected virtual async Task ResetOffsetAsync()
    {
        // offsets delivered before are obsolete, and must not overwrite the reset
        await this._commitLock.WaitAsync(this.CancellationTokenSource.Token).ConfigureAwait(false);
        try
        {
            lock (this._syncLock) this.DiscardUncommittedOffset();
            await this.CommitOffsetAsync(null).ConfigureAwait(false);
        }
        finally { this._commitLock.Release(); }
        // the offset has been changed by the user, which overrides whatever acknowledgement was pending
        var pendingAck = this.Subscription.Status?.Subscriber?.PendingAck;
        if (pendingAck != null)
        {
            this.EndAwaitingAcknowledgement(null);
            try { await this.ClearPendingAcknowledgementAsync(pendingAck.Token, null).ConfigureAwait(false); }
            catch (ProblemDetailsException) { }
        }
    }

    /// <summary>
    /// Initializes the <see cref="SubscriptionHandler"/>
    /// </summary>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    protected virtual async Task InitializeCloudEventStreamAsync()
    {
        using var activity = CloudStreamsDefaults.Telemetry.ActivitySource.StartActivity("SubscriptionHandler.InitializeStream");
        if (this.Subscription.Status?.Stream?.Fault != null || this.Subscription.Spec.Suspended) return;
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
            if (this.IsOffsetResetRequested()) await this.ResetOffsetAsync().ConfigureAwait(false);

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
                // the position reached by a previous session is meaningless once the stream is observed again, possibly at another offset
                lock (this._syncLock) { this._nextOffset = null; this._startOffset = null; }
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
                        // the stream does not exist yet: whatever it will contain is published after the subscription, which must therefore not start at its end, or it would
                        // skip the events published while it waits for the stream to exist
                        if (offset == StreamPosition.EndOfStream) lock (this._syncLock) this._startOffset = 0;
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
                        // the stream does not exist yet: whatever it will contain is published after the subscription, which must therefore not start at its end, or it would
                        // skip the events published while it waits for the stream to exist
                        if (offset == StreamPosition.EndOfStream) lock (this._syncLock) this._startOffset = 0;
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
            // a handler that starts behind the end of the stream catches up: it is marked out of sync before subscribing, otherwise the records replayed by the
            // subscription before the flag is set would be delivered both live and by the catch-up
            if (offset != StreamPosition.EndOfStream && (ulong)offset < (this.StreamOffset ?? 0)) lock (this._syncLock) this.SubscriptionOutOfSync = true;
            // every record is observed, so that the length of the stream is accurate: records are filtered once, when dispatched (see #162)
            var subscriptionHandle = observedStream.ToAsyncEnumerable().ToObservable().SubscribeAsync(this.OnCloudEventAsync, onErrorAsync: this.OnSubscriptionErrorAsync, null);
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
                    // a subscription delivering in batches or in parallel never defers acknowledgements (see #212)
                    var pendingAck = this.Subscription.Spec.Stream == null || this.IsPumped ? null : this.Subscription.Status?.Subscriber?.PendingAck;
                    if (pendingAck != null)
                    {
                        // the acknowledgement was deferred before the broker (re)started: delivery stays suspended until it is resolved or expires
                        lock (this._syncLock) this.BeginAwaitingAcknowledgement(pendingAck.Token, pendingAck.Offset, observed: true);
                        this.SubscriptionOutOfSync = true;
                        shouldCatchUp = false;
                    }
                    // a new stream starts in sync unless it has to catch up: a previous session aborted while out of sync must not wedge this one
                    else this.SubscriptionOutOfSync = shouldCatchUp;
                    initializationOutcome = StreamInitializationOutcomes.Initialized;
                    subscriptionHandle = null;
                }
            }
            finally { this.InitLock.Release(); }
            subscriptionHandle?.Dispose();
            if (initializationOutcome == StreamInitializationOutcomes.Initialized && this.IsPumped) _ = this.PumpAsync();
            else if (initializationOutcome == StreamInitializationOutcomes.Initialized && shouldCatchUp) _ = this.CatchUpAsync().ConfigureAwait(false);
            var restoredAck = this.Subscription.Status?.Subscriber?.PendingAck;
            if (initializationOutcome == StreamInitializationOutcomes.Initialized && restoredAck != null && this.AwaitingAcknowledgement && this.PendingAcknowledgementToken == restoredAck.Token) this.ScheduleAcknowledgementTimeout(restoredAck.Token, restoredAck.ExpiresAt);
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
    protected virtual ValueTask<bool> FiltersAsync(CloudEventRecord e) => this.Pipeline.FiltersAsync(this.Subscription, e, this.CancellationTokenSource.Token);

    /// <summary>
    /// Mutates the specified <see cref="CloudEvent"/>
    /// </summary>
    /// <param name="e">The <see cref="CloudEvent"/> to mutate</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>, which bounds the duration of a webhook based mutation</param>
    /// <returns>The mutated <see cref="CloudEvent"/></returns>
    protected virtual Task<CloudEvent> MutateAsync(CloudEvent e, CancellationToken cancellationToken = default)
    {
        if (!cancellationToken.CanBeCanceled) cancellationToken = this.CancellationTokenSource.Token;
        return this.Pipeline.MutateAsync(this.Subscription, e, cancellationToken);
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
        if (!await this.FiltersAsync(e).ConfigureAwait(false))
        {
            this.OnRecordHandled(e.Sequence);
            return true;
        }
        // mutations are applied by every delivery attempt, so that a webhook based mutation is bound by the dispatch timeout and goes through the retry policy
        var cloudEvent = e.ToCloudEvent(this.Broker.Resource.Spec.Dispatch?.Sequencing);
        var handled = await this.DispatchAsync(cloudEvent, e.Sequence, retryOnError, catchUpWhenAvailable).ConfigureAwait(false);
        // a subscription that does not track an offset drops an event that could not be delivered, and moves on
        if (handled || this.Subscription.Spec.Stream == null) this.OnRecordHandled(e.Sequence);
        return handled;
    }

    /// <summary>
    /// Records that the specified record has been handled, which is to say delivered, filtered out or dropped
    /// </summary>
    /// <param name="sequence">The sequence of the handled record</param>
    protected virtual void OnRecordHandled(ulong sequence)
    {
        lock (this._syncLock) if (!this._nextOffset.HasValue || sequence + 1 > this._nextOffset.Value) this._nextOffset = sequence + 1;
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
        await this.ThrottleAsync().ConfigureAwait(false);
        var timeout = this.GetDispatchTimeout();
        using var timeoutCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(this.CancellationTokenSource.Token);
        timeoutCancellationTokenSource.CancelAfter(timeout);
        // deferring the acknowledgement only makes sense for a subscription that tracks an offset, since there is otherwise nothing to advance or rewind
        var tracksOffset = this.Subscription.Spec.Stream != null;
        // a subscription delivering in batches or in parallel commits whole pages, and never defers acknowledgements (see #212)
        var pumped = this.IsPumped;
        var ackToken = tracksOffset && !pumped ? AcknowledgementTokens.Generate() : null;
        var deferred = false;
        var startedAt = Stopwatch.GetTimestamp();
        var outcome = DeliveryOutcome.Failed;
        try
        {
            e = await this.MutateAsync(e, timeoutCancellationTokenSource.Token).ConfigureAwait(false);
            this._lastAttempt = (offset, e);
            var headers = new List<KeyValuePair<string, string>>()
            {
                new(CloudStreamsHeaders.Subscription, this.Subscription.GetName()),
                new(CloudStreamsHeaders.Offset, offset.ToString(CultureInfo.InvariantCulture))
            };
            if (ackToken != null)
            {
                headers.Add(new(CloudStreamsHeaders.AckToken, ackToken));
                var ackUri = this.GetAcknowledgementUri();
                if (ackUri != null) headers.Add(new(CloudStreamsHeaders.AckUri, ackUri.OriginalString));
            }
            using var requestContent = e.ToHttpContent();
            var status = await this.Client.PostAsync(this.Subscription.Spec.Subscriber.Uri, requestContent, headers, "The server", timeoutCancellationTokenSource.Token).ConfigureAwait(false);
            // per RFC 9110, '202 Accepted' means that the request has been accepted for processing, but that the processing has not been completed (see #133)
            deferred = ackToken != null && status == HttpStatusCode.Accepted;
            outcome = deferred ? DeliveryOutcome.Deferred : DeliveryOutcome.Delivered;
        }
        catch (OperationCanceledException ex) when (!this.CancellationTokenSource.IsCancellationRequested)
        {
            var target = this.Subscription.Spec.Mutation?.Type == CloudEventMutationType.Webhook ? "The mutation webhook or the subscriber" : "The subscriber";
            throw new HttpRequestException($"{target} did not respond within the configured timeout of {timeout}", ex);
        }
        finally
        {
            // an attempt interrupted by the handler's disposal says nothing about the subscriber
            if (!this.CancellationTokenSource.IsCancellationRequested) this.Metrics.RecordDeliveryDuration(this.Subscription.GetQualifiedName(), this.GetStreamName(), outcome, Stopwatch.GetElapsedTime(startedAt));
        }
        this.Metrics.IncrementTotalPublishedEvents(this.Subscription.GetQualifiedName(), this.GetStreamName());
        await this.SetSubscriberStateAsync(SubscriberState.Reachable).ConfigureAwait(false);
        if (deferred)
        {
            // the subscriber's acknowledgement advances the offset itself: whatever was delivered before must be committed first, so as not to overwrite it
            await this.FlushOffsetAsync().ConfigureAwait(false);
            await this.SuspendForAcknowledgementAsync(offset, ackToken!).ConfigureAwait(false);
        }
        else if (tracksOffset && !pumped) await this.CommitDeliveredOffsetAsync(offset + 1).ConfigureAwait(false);
    }

    /// <summary>
    /// Waits, if necessary, until the subscription's rate limit allows the next delivery attempt
    /// </summary>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    /// <remarks>
    /// Attempts are spaced by the inverse of the rate limit, measured from the start of the previous attempt, so that the time spent delivering counts towards the
    /// interval and the configured rate is the actual delivery rate. A rate limit that is not strictly positive is ignored
    /// </remarks>
    protected virtual async Task ThrottleAsync()
    {
        var rateLimit = this.Subscription.Spec.Subscriber.RateLimit;
        if (rateLimit is not > 0 || double.IsInfinity(rateLimit.Value) || double.IsNaN(rateLimit.Value)) return;
        var intervalInSeconds = Math.Min(1 / rateLimit.Value, TimeSpan.FromDays(1).TotalSeconds);
        var intervalInTicks = (long)(intervalInSeconds * Stopwatch.Frequency);
        TimeSpan delay;
        lock (this._syncLock)
        {
            var now = Stopwatch.GetTimestamp();
            var next = Math.Max(now, this._nextDeliveryTimestamp);
            delay = next > now ? Stopwatch.GetElapsedTime(now, next) : TimeSpan.Zero;
            this._nextDeliveryTimestamp = next + intervalInTicks;
        }
        if (delay > TimeSpan.Zero) await Task.Delay(delay, this.CancellationTokenSource.Token).ConfigureAwait(false);
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
        var schedule = new DeliveryRetrySchedule(this.Subscription.Spec.Subscriber.RetryPolicy ?? this.DefaultRetryPolicy);
        var lastFailure = initialFailure;
        try
        {
            this.SubscriptionOutOfSync = true;
            this.Metrics.IncrementTotalDeliveryFailures(this.Subscription.GetQualifiedName(), streamName);
            await this.SetSubscriberStateAsync(SubscriberState.Unreachable, lastFailure.Message).ConfigureAwait(false);
            while (true)
            {
                if (!schedule.IsRetryable(lastFailure)) throw new HttpRequestException($"Failed to deliver the cloud event at offset '{offset}' to subscriber '{subscriberUri}': the subscriber responded with status code '{lastFailure.StatusCode}', which the retry policy is not configured to retry. {lastFailure.Message}. {DescribeHowToResume(offset)}", lastFailure, lastFailure.StatusCode);
                if (schedule.IsExhausted) throw new HttpRequestException($"Failed to deliver the cloud event at offset '{offset}' to subscriber '{subscriberUri}' after {schedule.Attempts + 1} attempt(s). Last failure: {lastFailure.Message}. {DescribeHowToResume(offset)}", lastFailure, lastFailure.StatusCode);
                var consecutiveFailures = schedule.ConsecutiveFailures;
                var delay = schedule.Next(out var circuitBroken);
                if (circuitBroken) this.Logger.LogWarning("The circuit of subscription '{subscription}' has been broken after {failures} consecutive delivery failures. Delivery will resume in {delay}", this.Subscription.GetQualifiedName(), consecutiveFailures, delay);
                if (delay > TimeSpan.Zero) await Task.Delay(delay, this.CancellationTokenSource.Token).ConfigureAwait(false);
                try
                {
                    await this.DeliverAsync(e, offset).ConfigureAwait(false);
                    break;
                }
                catch (HttpRequestException ex)
                {
                    lastFailure = ex;
                    schedule.RecordFailure();
                    this.Metrics.IncrementTotalDeliveryFailures(this.Subscription.GetQualifiedName(), streamName);
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
            var failure = ex;
            if (this.Subscription.Spec.DeadLetter != null)
            {
                Exception? deadLetterFailure = null;
                try { await this.DeadLetterAsync(e, offset, ex).ConfigureAwait(false); }
                catch (Exception inner) when (inner is not OperationCanceledException || !this.CancellationTokenSource.IsCancellationRequested) { deadLetterFailure = inner; }
                if (deadLetterFailure == null)
                {
                    // the cloud event has been set aside: delivery carries on with the next one, as if it had been delivered (see #207)
                    if (this.Subscription.Spec.Stream != null && !this.IsPumped) await this.CommitDeliveredOffsetAsync(offset + 1).ConfigureAwait(false);
                    if (catchUpWhenAvailable) await this.CatchUpAsync().ConfigureAwait(false);
                    return true;
                }
                this.Logger.LogError(deadLetterFailure, "Failed to dead-letter the cloud event at offset '{offset}' of subscription '{subscription}'", offset, this.Subscription.GetQualifiedName());
                failure = new HttpRequestException($"{ex.Message}. The cloud event could not be dead-lettered either: {deadLetterFailure.Message}", ex);
            }
            if (this.Subscription.Spec.Stream == null)
            {
                this.Logger.LogWarning("Dropped the cloud event at offset '{offset}' of subscription '{subscription}', which does not track a stream offset: {reason}", offset, this.Subscription.GetQualifiedName(), failure.Message);
                this.SubscriptionOutOfSync = false;
            }
            else await this.OnSubscriptionErrorAsync(failure).ConfigureAwait(false);
            return false;
        }
    }

    /// <summary>
    /// Sets aside the specified cloud event, which could not be delivered, as configured by the subscription's dead-letter policy
    /// </summary>
    /// <param name="e">The cloud event that could not be delivered</param>
    /// <param name="offset">The offset of the cloud event</param>
    /// <param name="reason">The exception that describes why the cloud event could not be delivered</param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    /// <remarks>
    /// The cloud event is dead-lettered as it was last sent to the subscriber, which is to say mutated, if the subscription mutates cloud events, so that it can be
    /// redelivered as is (see #207)
    /// </remarks>
    protected virtual async Task DeadLetterAsync(CloudEvent e, ulong offset, Exception reason)
    {
        var policy = this.Subscription.Spec.DeadLetter ?? throw new InvalidOperationException($"The subscription '{this.Subscription.GetQualifiedName()}' has no dead-letter policy");
        var deadLetter = new DeadLetter()
        {
            EventOffset = offset,
            Event = this._lastAttempt is { } attempt && attempt.Offset == offset ? attempt.Event : e,
            Reason = reason.ToProblemDetails(),
            DeadLetteredAt = DateTimeOffset.UtcNow
        };
        if (policy.Uri == null) await this.DeadLetters.AppendAsync(this.Subscription.GetQualifiedName(), deadLetter, this.CancellationTokenSource.Token).ConfigureAwait(false);
        else await this.SendDeadLetterAsync(policy.Uri, deadLetter).ConfigureAwait(false);
        this.Metrics.IncrementTotalDeadLetters(this.Subscription.GetQualifiedName(), this.GetStreamName());
        this.Logger.LogWarning("Dead-lettered the cloud event at offset '{offset}' of subscription '{subscription}': {reason}", offset, this.Subscription.GetQualifiedName(), reason.Message);
    }

    /// <summary>
    /// Gets the maximum amount of attempts at sending a cloud event to a dead-letter service
    /// </summary>
    protected const int MaxDeadLetterAttempts = 3;

    /// <summary>
    /// Sends the specified dead letter to the specified dead-letter service, as a structured cloud event, retrying a few times if it fails
    /// </summary>
    /// <param name="uri">The uri of the dead-letter service</param>
    /// <param name="deadLetter">The dead letter to send</param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    /// <exception cref="HttpRequestException">The dead-letter service could not be reached, or did not accept the dead letter</exception>
    protected virtual async Task SendDeadLetterAsync(Uri uri, DeadLetter deadLetter)
    {
        var reason = deadLetter.Reason.Detail ?? deadLetter.Reason.Title;
        if (reason.Length > 1024) reason = reason[..1024];
        for (var attempt = 1; ; attempt++)
        {
            var timeout = this.GetDispatchTimeout();
            using var timeoutCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(this.CancellationTokenSource.Token);
            timeoutCancellationTokenSource.CancelAfter(timeout);
            try
            {
                using var content = deadLetter.Event.ToHttpContent();
                KeyValuePair<string, string>[] headers =
                [
                    new(CloudStreamsHeaders.Subscription, this.Subscription.GetName()),
                    new(CloudStreamsHeaders.Offset, deadLetter.EventOffset.ToString(CultureInfo.InvariantCulture)),
                    new(CloudStreamsHeaders.DeadLetterReason, Uri.EscapeDataString(reason))
                ];
                await this.Client.PostAsync(uri, content, headers, $"The dead-letter service '{uri}'", timeoutCancellationTokenSource.Token).ConfigureAwait(false);
                return;
            }
            catch (OperationCanceledException ex) when (!this.CancellationTokenSource.IsCancellationRequested && attempt >= MaxDeadLetterAttempts)
            {
                throw new HttpRequestException($"The dead-letter service '{uri}' did not respond within the configured timeout of {timeout}", ex);
            }
            catch (Exception ex) when (attempt < MaxDeadLetterAttempts && (ex is HttpRequestException || (ex is OperationCanceledException && !this.CancellationTokenSource.IsCancellationRequested)))
            {
                this.Logger.LogWarning("Failed to send a dead letter of subscription '{subscription}' to '{uri}' (attempt {attempt}/{maxAttempts}): {reason}", this.Subscription.GetQualifiedName(), uri, attempt, MaxDeadLetterAttempts, ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt - 1)), this.CancellationTokenSource.Token).ConfigureAwait(false);
            }
        }
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
    /// Gets the subscription's lag, which is to say the amount of records of its stream it has yet to handle
    /// </summary>
    /// <returns>The name of the stream the subscription reads from and its lag, or null if the length of the stream is not known yet</returns>
    /// <remarks>
    /// The position of the subscription is the offset of the event whose acknowledgement is pending, if any. Otherwise, it is the furthest of its acknowledged offset
    /// and of the offset that follows the last record it handled, since records that are filtered out are handled without advancing the acknowledged offset.
    /// The length of the stream is updated by every record the handler observes, and records that are filtered out are handled as soon as they are observed
    /// </remarks>
    protected virtual (string Stream, ulong Lag)? GetLag()
    {
        ulong? length, position;
        lock (this._syncLock)
        {
            length = this.StreamOffset;
            if (this.AwaitingAcknowledgement) position = this.PendingAcknowledgementOffset;
            else
            {
                var ackedOffset = this.Subscription.Status?.Stream?.AckedOffset;
                position = ackedOffset.HasValue && this._nextOffset.HasValue ? Math.Max(ackedOffset.Value, this._nextOffset.Value) : ackedOffset ?? this._nextOffset;
                if (!position.HasValue && this.Subscription.Spec.Stream?.Offset >= 0) position = (ulong)this.Subscription.Spec.Stream.Offset.Value;
            }
        }
        if (!length.HasValue) return null;
        // a subscription that has handled nothing yet and starts at the end of the stream is up to date
        var lag = position.HasValue && length.Value > position.Value ? length.Value - position.Value : 0;
        return (this.GetStreamName(), lag);
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
    protected virtual Task CatchUpAsync() => this.CatchUpAsync(null);

    /// <summary>
    /// Catches up missed <see cref="CloudEvent"/>s, starting at the specified offset
    /// </summary>
    /// <param name="fromOffset">The offset to start catching up at. If not set, resumes from the subscription's acknowledged offset</param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    protected virtual async Task CatchUpAsync(long? fromOffset)
    {
        using var activity = CloudStreamsDefaults.Telemetry.ActivitySource.StartActivity("SubscriptionHandler.CatchUp");
        activity?.SetTag("subscription", this.Subscription.GetQualifiedName());
        var streamInitializationToken = CancellationToken.None;
        var streamInitializationSessionId = 0L;
        TaskCompletionSource? streamSynchronizationCompletionSource = null;
        await this.InitLock.WaitAsync(this.CancellationTokenSource.Token).ConfigureAwait(false);
        try
        {
            // a subscription awaiting an acknowledgement must not dispatch anything else: it resumes once the acknowledgement is received or expires
            if (this.StreamInitializationCancellationTokenSource == null || this.AwaitingAcknowledgement) return;
            streamInitializationToken = this.StreamInitializationCancellationTokenSource.Token;
            streamInitializationSessionId = Interlocked.Read(ref this._streamInitializationSessionId);
            this.StreamSynchronizationTaskCompletionSource ??= new(TaskCreationOptions.RunContinuationsAsynchronously);
            streamSynchronizationCompletionSource = this.StreamSynchronizationTaskCompletionSource;
        }
        finally { this.InitLock.Release(); }
        try
        {
            this.SubscriptionOutOfSync = true;
            var currentOffset = fromOffset ?? this.Subscription.GetOffset();
            if (!fromOffset.HasValue && this.CommittedOffset.HasValue && this.CommittedOffsetGeneration == this.Subscription.Metadata.Generation && (long)this.CommittedOffset.Value > currentOffset) currentOffset = (long)this.CommittedOffset.Value;
            if (currentOffset == StreamPosition.EndOfStream)
            {
                // the end of the stream is the position after its last record: resume after the last record the handler has dealt with, if any, and otherwise
                // start at the stream's current length, so that a new subscription never receives an event published before it was created (see #155)
                ulong? nextOffset;
                lock (this._syncLock) nextOffset = this._nextOffset;
                ulong? startOffset;
                lock (this._syncLock) startOffset = this._startOffset;
                currentOffset = (long)(nextOffset ?? startOffset ?? this.StreamOffset ?? 0);
            }
            // records are read a page at a time, and the next page is read while the current one is being delivered (see #162)
            var reader = new StreamPageReader(this.ReadPageAsync, this.Options.CatchUp.GetPageSize());
            while (true)
            {
                if (!this.IsStreamInitializationSessionCurrent(streamInitializationSessionId)) return;
                await foreach (var page in reader.ReadPagesAsync(currentOffset, () => this.StreamOffset ?? 0, streamInitializationToken).ConfigureAwait(false))
                {
                    foreach (var record in page)
                    {
                        if (streamInitializationToken.IsCancellationRequested || !this.IsStreamInitializationSessionCurrent(streamInitializationSessionId)) return;
                        if (!await this.DispatchAsync(record, true, false).ConfigureAwait(false) || this.AwaitingAcknowledgement) return;
                        currentOffset++;
                    }
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
                    // a subscription awaiting an acknowledgement stays out of sync, so that live events are dropped until it resumes
                    this.SubscriptionOutOfSync = this.AwaitingAcknowledgement;
                    if (this.StreamSynchronizationTaskCompletionSource == streamSynchronizationCompletionSource) this.StreamSynchronizationTaskCompletionSource = null;
                }
            }
            finally { this.InitLock.Release(); }
            streamSynchronizationCompletionSource?.TrySetResult();
        }
    }

    /// <summary>
    /// Gets a boolean indicating whether or not the subscription delivers cloud events in batches or in parallel, in which case it reads what it delivers from the
    /// store, a page at a time, rather than delivering live records as they come (see #212)
    /// </summary>
    protected bool IsPumped => this.Subscription.Spec.Subscriber.Batch != null || this.Subscription.Spec.Subscriber.Ordering != null;

    /// <summary>
    /// Tells the pump that there are more records to read
    /// </summary>
    protected void SignalPump()
    {
        try { if (this._pumpSignal.CurrentCount == 0) this._pumpSignal.Release(); }
        catch (SemaphoreFullException) { }
    }

    /// <summary>
    /// Delivers the records of the stream a page at a time, in batches or in parallel, and commits the offset that follows each page once all of its records have been
    /// handled (see #212)
    /// </summary>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    /// <remarks>
    /// <para>
    /// The pump replaces both live delivery and catch-up: live records only tell it that there is more to read. A page is only committed once each of its records has
    /// been delivered, dead-lettered or filtered out, so that the committed offset never moves past a record that has not been, whatever the order its records were
    /// delivered in. A broker that stops in the middle of a page redelivers the whole page: delivery remains at-least-once.
    /// </para>
    /// <para>Subscribers cannot defer acknowledgements: <c>202 Accepted</c> is a success like any other</para>
    /// </remarks>
    protected virtual async Task PumpAsync()
    {
        using var activity = CloudStreamsDefaults.Telemetry.ActivitySource.StartActivity("SubscriptionHandler.Pump");
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
            var position = this.Subscription.GetOffset();
            if (this.CommittedOffset.HasValue && this.CommittedOffsetGeneration == this.Subscription.Metadata.Generation && (long)this.CommittedOffset.Value > position) position = (long)this.CommittedOffset.Value;
            // as with catch-up, the end of the stream is the position after the last record dealt with, or the stream's current length (see #155)
            if (position == StreamPosition.EndOfStream) lock (this._syncLock) position = (long)(this._nextOffset ?? this._startOffset ?? this.StreamOffset ?? 0);
            while (!streamInitializationToken.IsCancellationRequested && this.IsStreamInitializationSessionCurrent(streamInitializationSessionId))
            {
                var available = (long)(this.StreamOffset ?? 0) - position;
                if (available <= 0)
                {
                    await this._pumpSignal.WaitAsync(streamInitializationToken).ConfigureAwait(false);
                    continue;
                }
                var batch = this.Subscription.Spec.Subscriber.Batch;
                var pageSize = batch != null ? Math.Max(1, batch.MaxSize) : this.Options.CatchUp.GetPageSize();
                var linger = batch?.Linger?.ToTimeSpan() ?? SubscriberBatchConfiguration.DefaultLinger;
                if (batch != null && linger > TimeSpan.Zero && available < pageSize)
                {
                    // a partial batch waits for more records, for at most the linger time
                    var lingerUntil = DateTimeOffset.UtcNow + linger;
                    while ((long)(this.StreamOffset ?? 0) - position < pageSize)
                    {
                        var remaining = lingerUntil - DateTimeOffset.UtcNow;
                        if (remaining <= TimeSpan.Zero) break;
                        await this._pumpSignal.WaitAsync(remaining, streamInitializationToken).ConfigureAwait(false);
                    }
                }
                var page = await this.ReadPageAsync(position, pageSize, streamInitializationToken).ConfigureAwait(false);
                if (page.Count == 0)
                {
                    await Task.Delay(StreamPageReader.UnreadableRecordDelay, streamInitializationToken).ConfigureAwait(false);
                    continue;
                }
                var handled = batch != null
                    ? await this.DeliverBatchAsync(page, streamInitializationToken).ConfigureAwait(false)
                    : await this.DeliverInParallelAsync(page, streamInitializationToken).ConfigureAwait(false);
                if (!handled || streamInitializationToken.IsCancellationRequested || !this.IsStreamInitializationSessionCurrent(streamInitializationSessionId)) return;
                var last = page[^1].Sequence;
                if (this.Subscription.Spec.Stream != null) await this.CommitDeliveredOffsetAsync(last + 1).ConfigureAwait(false);
                this.OnRecordHandled(last);
                position = (long)last + 1;
            }
        }
        catch (Exception ex) when (ex is ObjectDisposedException || ex is TaskCanceledException || ex is OperationCanceledException || (ex is RpcException rpcException && rpcException.StatusCode == StatusCode.Cancelled)) { }
        catch (Exception ex) { await this.OnSubscriptionErrorAsync(ex).ConfigureAwait(false); }
        finally
        {
            await this.InitLock.WaitAsync(CancellationToken.None).ConfigureAwait(false);
            try
            {
                if (this.StreamSynchronizationTaskCompletionSource == streamSynchronizationCompletionSource) this.StreamSynchronizationTaskCompletionSource = null;
            }
            finally { this.InitLock.Release(); }
            streamSynchronizationCompletionSource?.TrySetResult();
        }
    }

    /// <summary>
    /// Delivers the records of the specified page that the subscription selects as a single batch, retrying it as configured by the subscription's retry policy
    /// </summary>
    /// <param name="page">The page of records to deliver</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A boolean indicating whether or not every record of the page has been handled, which is to say delivered, dead-lettered or filtered out</returns>
    /// <remarks>
    /// A batch whose delivery fails for good is delivered again one cloud event at a time, each with the retry and dead-letter policies, so that the cloud event at
    /// fault, if any, is dead-lettered, or faults the subscription, on its own
    /// </remarks>
    protected virtual async Task<bool> DeliverBatchAsync(IReadOnlyList<CloudEventRecord> page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        var records = new List<CloudEventRecord>(page.Count);
        foreach (var record in page) if (await this.FiltersAsync(record).ConfigureAwait(false)) records.Add(record);
        if (records.Count == 0) return true;
        DeliveryRetrySchedule? schedule = null;
        while (true)
        {
            try
            {
                await this.SendBatchAsync(records).ConfigureAwait(false);
                return true;
            }
            catch (HttpRequestException ex)
            {
                if (schedule == null) schedule = new(this.Subscription.Spec.Subscriber.RetryPolicy ?? this.DefaultRetryPolicy);
                else schedule.RecordFailure();
                this.Metrics.IncrementTotalDeliveryFailures(this.Subscription.GetQualifiedName(), this.GetStreamName());
                await this.SetSubscriberStateAsync(SubscriberState.Unreachable, ex.Message).ConfigureAwait(false);
                if (!schedule.IsRetryable(ex) || schedule.IsExhausted)
                {
                    this.Logger.LogWarning("Failed to deliver the batch of {count} cloud event(s) at offsets '{first}' to '{last}' of subscription '{subscription}': delivering them one at a time. Last failure: {reason}", records.Count, records[0].Sequence, records[^1].Sequence, this.Subscription.GetQualifiedName(), ex.Message);
                    break;
                }
                var consecutiveFailures = schedule.ConsecutiveFailures;
                var delay = schedule.Next(out var circuitBroken);
                if (circuitBroken) this.Logger.LogWarning("The circuit of subscription '{subscription}' has been broken after {failures} consecutive delivery failures. Delivery will resume in {delay}", this.Subscription.GetQualifiedName(), consecutiveFailures, delay);
                if (delay > TimeSpan.Zero) await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }
        var sequencing = this.Broker.Resource.Spec.Dispatch?.Sequencing;
        foreach (var record in records)
        {
            if (cancellationToken.IsCancellationRequested) return false;
            if (!await this.DispatchAsync(record.ToCloudEvent(sequencing), record.Sequence, true, false).ConfigureAwait(false) && this.Subscription.Spec.Stream != null) return false;
        }
        return true;
    }

    /// <summary>
    /// Performs a single attempt at delivering the specified records as a batch, formatted as <c>application/cloudevents-batch+json</c>
    /// </summary>
    /// <param name="records">The records to deliver, which the subscription selects</param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    /// <exception cref="HttpRequestException">The delivery attempt failed</exception>
    /// <remarks>The batch carries the offset of its first cloud event. Cloud events are mutated by every attempt, which a webhook based mutation is bound by the dispatch timeout of</remarks>
    protected virtual async Task SendBatchAsync(IReadOnlyList<CloudEventRecord> records)
    {
        await this.ThrottleAsync().ConfigureAwait(false);
        var timeout = this.GetDispatchTimeout();
        using var timeoutCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(this.CancellationTokenSource.Token);
        timeoutCancellationTokenSource.CancelAfter(timeout);
        var startedAt = Stopwatch.GetTimestamp();
        var outcome = DeliveryOutcome.Failed;
        var sequencing = this.Broker.Resource.Spec.Dispatch?.Sequencing;
        try
        {
            var events = new List<CloudEvent>(records.Count);
            foreach (var record in records) events.Add(await this.MutateAsync(record.ToCloudEvent(sequencing), timeoutCancellationTokenSource.Token).ConfigureAwait(false));
            KeyValuePair<string, string>[] headers =
            [
                new(CloudStreamsHeaders.Subscription, this.Subscription.GetName()),
                new(CloudStreamsHeaders.Offset, records[0].Sequence.ToString(CultureInfo.InvariantCulture))
            ];
            using var content = events.ToBatchHttpContent();
            await this.Client.PostAsync(this.Subscription.Spec.Subscriber.Uri, content, headers, "The server", timeoutCancellationTokenSource.Token).ConfigureAwait(false);
            outcome = DeliveryOutcome.Delivered;
        }
        catch (OperationCanceledException ex) when (!this.CancellationTokenSource.IsCancellationRequested)
        {
            var target = this.Subscription.Spec.Mutation?.Type == CloudEventMutationType.Webhook ? "The mutation webhook or the subscriber" : "The subscriber";
            throw new HttpRequestException($"{target} did not respond within the configured timeout of {timeout}", ex);
        }
        finally
        {
            if (!this.CancellationTokenSource.IsCancellationRequested) this.Metrics.RecordDeliveryDuration(this.Subscription.GetQualifiedName(), this.GetStreamName(), outcome, Stopwatch.GetElapsedTime(startedAt));
        }
        for (var i = 0; i < records.Count; i++) this.Metrics.IncrementTotalPublishedEvents(this.Subscription.GetQualifiedName(), this.GetStreamName());
        await this.SetSubscriberStateAsync(SubscriberState.Reachable).ConfigureAwait(false);
    }

    /// <summary>
    /// Delivers the records of the specified page in parallel: records that share the same key are delivered one at a time, in the order of the stream, while records
    /// of different keys are delivered concurrently
    /// </summary>
    /// <param name="page">The page of records to deliver</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A boolean indicating whether or not every record of the page has been handled, which is to say delivered, dead-lettered or filtered out</returns>
    protected virtual async Task<bool> DeliverInParallelAsync(IReadOnlyList<CloudEventRecord> page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        var ordering = this.Subscription.Spec.Subscriber.Ordering ?? throw new InvalidOperationException($"The subscription '{this.Subscription.GetQualifiedName()}' does not deliver cloud events in parallel");
        using var concurrency = new SemaphoreSlim(Math.Max(1, ordering.MaxConcurrency ?? SubscriberOrderingConfiguration.DefaultMaxConcurrency));
        // grouping preserves the order of the records of each key
        var keys = page.GroupBy(r => r.Metadata.ContextAttributes.TryGetValue(ordering.Key, out var key) ? key?.ToString() ?? string.Empty : string.Empty);
        var results = await Task.WhenAll(keys.Select(async records =>
        {
            await concurrency.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                foreach (var record in records)
                {
                    if (cancellationToken.IsCancellationRequested) return false;
                    if (!await this.DispatchAsync(record, true, false).ConfigureAwait(false) && this.Subscription.Spec.Stream != null) return false;
                }
                return true;
            }
            finally { concurrency.Release(); }
        })).ConfigureAwait(false);
        return results.All(handled => handled);
    }

    /// <summary>
    /// Reads a page of the records the subscription reads from
    /// </summary>
    /// <param name="offset">The offset of the first record to read</param>
    /// <param name="length">The maximum amount of records to read</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The records read, which may be fewer than requested, or none, if the end of the stream has been reached</returns>
    protected virtual Task<List<CloudEventRecord>> ReadPageAsync(long offset, int length, CancellationToken cancellationToken)
    {
        var records = this.Subscription.Spec.Partition == null
            ? this.EventStore.ReadAsync(StreamReadDirection.Forwards, offset, (ulong)length, cancellationToken)
            : this.EventStore.ReadPartitionAsync(this.Subscription.Spec.Partition, StreamReadDirection.Forwards, offset, (ulong)length, cancellationToken);
        return records.ToListAsync(cancellationToken).AsTask();
    }

    /// <summary>
    /// Gets the maximum amount of time a cloud event whose acknowledgement has been deferred may remain unacknowledged
    /// </summary>
    /// <returns>The maximum amount of time a cloud event whose acknowledgement has been deferred may remain unacknowledged</returns>
    protected virtual TimeSpan GetAcknowledgementTimeout()
    {
        var timeout = this.Subscription.Spec.Subscriber.Ack?.Timeout?.ToTimeSpan() ?? this.Broker.Resource.Spec.Dispatch?.Ack?.Timeout?.ToTimeSpan() ?? SubscriberAcknowledgementConfiguration.DefaultTimeout;
        return timeout > TimeSpan.Zero ? timeout : SubscriberAcknowledgementConfiguration.DefaultTimeout;
    }

    /// <summary>
    /// Gets the uri subscribers must call to acknowledge the cloud events delivered for the subscription, if the broker knows it
    /// </summary>
    /// <returns>The uri subscribers must call to acknowledge the cloud events delivered for the subscription, if any</returns>
    protected virtual Uri? GetAcknowledgementUri()
    {
        var callbackUri = this.Broker.Resource.Spec.Dispatch?.Ack?.CallbackUri;
        if (callbackUri == null) return null;
        return new Uri($"{callbackUri.OriginalString.TrimEnd('/')}/api/resources/v1/subscriptions/{Uri.EscapeDataString(this.Subscription.GetName())}/ack");
    }

    /// <summary>
    /// Suspends delivery until the subscriber acknowledges the specified cloud event, negatively acknowledges it, or the acknowledgement expires
    /// </summary>
    /// <param name="offset">The offset of the cloud event whose acknowledgement has been deferred</param>
    /// <param name="token">The token the subscriber must present to acknowledge the cloud event. Only its hash is recorded</param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    /// <remarks>
    /// The pending acknowledgement is recorded in the subscription's status, where the gateway validates the subscriber's callback. Suspension relies on the out-of-sync
    /// state rather than on blocking, so that live events are dropped, and later replayed by catch-up, instead of piling up in memory
    /// </remarks>
    protected virtual async Task SuspendForAcknowledgementAsync(ulong offset, string token)
    {
        // only the hash of the token is kept, and recorded in the status: the subscriber is the only one to know the token itself (see #149)
        token = AcknowledgementTokens.Hash(token);
        using var activity = CloudStreamsDefaults.Telemetry.ActivitySource.StartActivity("SubscriptionHandler.SuspendForAcknowledgement");
        activity?.SetTag("subscription", this.Subscription.GetQualifiedName());
        activity?.SetTag("offset", offset);
        var expiresAt = DateTimeOffset.UtcNow + this.GetAcknowledgementTimeout();
        lock (this._syncLock)
        {
            this.BeginAwaitingAcknowledgement(token, offset, observed: false);
            this.SubscriptionOutOfSync = true;
        }
        await this.PatchStatusAsync(resource =>
        {
            resource.Status ??= new() { ObservedGeneration = this.Subscription.Metadata.Generation };
            resource.Status.Subscriber ??= new();
            resource.Status.Subscriber.PendingAck = new() { Offset = offset, Token = token, ExpiresAt = expiresAt };
        }).ConfigureAwait(false);
        this.ScheduleAcknowledgementTimeout(token, expiresAt);
    }

    /// <summary>
    /// Marks the subscription as awaiting the acknowledgement of a cloud event
    /// </summary>
    void BeginAwaitingAcknowledgement(string token, ulong offset, bool observed) => this.Acknowledgements.Begin(token, offset, observed);

    /// <summary>
    /// Stops awaiting the acknowledgement of a cloud event
    /// </summary>
    /// <param name="token">The token of the pending acknowledgement to stop awaiting. If not set, stops awaiting whatever acknowledgement is pending</param>
    /// <returns>A boolean indicating whether or not the handler was awaiting the specified acknowledgement</returns>
    protected virtual bool EndAwaitingAcknowledgement(string? token) => this.EndAwaitingAcknowledgement(token, out _);

    /// <summary>
    /// Stops awaiting the acknowledgement of a cloud event
    /// </summary>
    /// <param name="token">The token of the pending acknowledgement to stop awaiting. If not set, stops awaiting whatever acknowledgement is pending</param>
    /// <param name="pendingFor">How long the acknowledgement has been pending, if known</param>
    /// <returns>A boolean indicating whether or not the handler was awaiting the specified acknowledgement</returns>
    protected virtual bool EndAwaitingAcknowledgement(string? token, out TimeSpan? pendingFor) => this.Acknowledgements.End(token, out pendingFor);

    /// <summary>
    /// Schedules the expiry of the specified pending acknowledgement
    /// </summary>
    /// <param name="token">The token of the pending acknowledgement</param>
    /// <param name="expiresAt">The date and time at which the pending acknowledgement expires</param>
    protected virtual void ScheduleAcknowledgementTimeout(string token, DateTimeOffset expiresAt) => this.Acknowledgements.ScheduleTimeout(token, expiresAt, this.CancellationTokenSource.Token);

    /// <summary>
    /// Handles changes to the subscription's pending acknowledgement, as reported by the resource watch
    /// </summary>
    /// <param name="pendingAck">The subscription's pending acknowledgement, if any</param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    /// <remarks>
    /// The watch lags behind the handler's own patches, so it may still report no pending acknowledgement right after the handler has suspended delivery.
    /// Delivery therefore only resumes once the watch has reported the pending acknowledgement, and then reports it gone, which means that the subscriber has
    /// called back: after an acknowledgement, the acknowledged offset has been advanced; after a negative acknowledgement, it has not, and the event is redelivered
    /// </remarks>
    protected virtual async Task OnPendingAcknowledgementChangedAsync(PendingAcknowledgement? pendingAck)
    {
        try
        {
            if (pendingAck != null)
            {
                this.Acknowledgements.MarkObserved(pendingAck.Token);
                return;
            }
            if (!this.Acknowledgements.TryGetObserved(out var offset)) return;
            if (!this.EndAwaitingAcknowledgement(null, out var pendingFor)) return;
            var ackedOffset = this.Subscription.Status?.Stream?.AckedOffset;
            // the subscriber acknowledged the event if the acknowledged offset has moved past it, and negatively acknowledged it otherwise
            if (pendingFor.HasValue) this.Metrics.RecordAcknowledgementDuration(this.Subscription.GetQualifiedName(), this.GetStreamName(), ackedOffset.HasValue && ackedOffset.Value > offset ? AcknowledgementOutcome.Acknowledged : AcknowledgementOutcome.NegativelyAcknowledged, pendingFor.Value);
            await this.CatchUpAsync(ackedOffset.HasValue && ackedOffset.Value > offset ? (long)ackedOffset.Value : (long)offset).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is OperationCanceledException or TaskCanceledException || (ex is RpcException rpcException && rpcException.StatusCode == StatusCode.Cancelled)) { }
        catch (Exception ex) { await this.OnSubscriptionErrorAsync(ex).ConfigureAwait(false); }
    }

    /// <summary>
    /// Handles the expiry of the specified pending acknowledgement
    /// </summary>
    /// <param name="token">The token of the pending acknowledgement that has expired</param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    /// <remarks>
    /// An expired acknowledgement is treated as a negative one, and the event is redelivered, so that a subscriber that defers and then dies can never hold its
    /// subscription suspended forever. A subscriber that has never called back, however, most likely misuses <c>202 Accepted</c> to mean "done": rather than
    /// letting it degrade into a silent redelivery loop, the subscription is faulted with a self-explaining reason
    /// </remarks>
    protected virtual async Task OnAcknowledgementTimeoutAsync(string token)
    {
        try
        {
            if (!this.Acknowledgements.TryGet(token, out var offset)) return;
            var neverAcknowledged = this.Subscription.Status?.Subscriber?.LastAcknowledgedAt == null;
            var timeout = this.GetAcknowledgementTimeout();
            if (!this.EndAwaitingAcknowledgement(token, out var pendingFor)) return;
            this.Metrics.IncrementTotalAcknowledgementTimeouts(this.Subscription.GetQualifiedName(), this.GetStreamName());
            if (pendingFor.HasValue) this.Metrics.RecordAcknowledgementDuration(this.Subscription.GetQualifiedName(), this.GetStreamName(), AcknowledgementOutcome.Expired, pendingFor.Value);
            var reason = neverAcknowledged
                ? $"The subscriber returned '202 Accepted' at offset '{offset}' but never acknowledged it within {timeout}. If it did not intend to defer the acknowledgement, it should return '200 OK' instead"
                : $"The subscriber did not acknowledge the cloud event at offset '{offset}' within {timeout}: it is being redelivered";
            this.Logger.LogWarning("Subscription '{subscription}': {reason}", this.Subscription.GetQualifiedName(), reason);
            try
            {
                await this.ClearPendingAcknowledgementAsync(token, reason).ConfigureAwait(false);
            }
            catch (ProblemDetailsException)
            {
                // the subscriber called back concurrently: whatever it decided is now recorded, so resume from the subscription's acknowledged offset
                await this.CatchUpAsync(null).ConfigureAwait(false);
                return;
            }
            if (neverAcknowledged) await this.OnSubscriptionErrorAsync(new TimeoutException(reason)).ConfigureAwait(false);
            else await this.CatchUpAsync((long)offset).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is OperationCanceledException or TaskCanceledException || (ex is RpcException rpcException && rpcException.StatusCode == StatusCode.Cancelled)) { }
        catch (Exception ex) { await this.OnSubscriptionErrorAsync(ex).ConfigureAwait(false); }
    }

    /// <summary>
    /// Removes the specified pending acknowledgement from the subscription's status, unless it has been resolved in the meantime
    /// </summary>
    /// <param name="token">The token of the pending acknowledgement to remove</param>
    /// <param name="reason">The reason to report on the subscriber's status, if any</param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    /// <exception cref="ProblemDetailsException">The pending acknowledgement has been resolved in the meantime</exception>
    protected virtual async Task ClearPendingAcknowledgementAsync(string token, string? reason)
    {
        var operations = new List<PatchOperation>()
        {
            // guards against a subscriber that calls back concurrently: the patch only applies if the acknowledgement is still pending
            PatchOperation.Test(JsonPointer.Parse("/status/subscriber/pendingAck/token"), JsonValue.Create(token)),
            PatchOperation.Remove(JsonPointer.Parse("/status/subscriber/pendingAck"))
        };
        if (reason != null) operations.Add(PatchOperation.Add(JsonPointer.Parse("/status/subscriber/reason"), JsonValue.Create(reason)));
        // the 'test' operation is checked atomically with the write, and is the only guard needed: no resource version is expected
        var patched = await this.ResourceRepository.PatchStatusAsync<Subscription>(new Patch(PatchType.JsonPatch, new JsonPatch(operations)), this.Subscription.GetName(), this.Subscription.GetNamespace(), null, false, this.CancellationTokenSource.Token).ConfigureAwait(false);
        if (patched != null) this.StatusReporter.Latest = patched;
        if (this.Subscription.Status?.Subscriber != null) this.Subscription.Status.Subscriber.PendingAck = null;
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
        var generation = this.Subscription.Metadata.Generation;
        this.CommittedOffset = offset;
        this.CommittedOffsetGeneration = generation;
        await this.PatchStatusAsync(resource =>
        {
            resource.Status ??= new() { ObservedGeneration = generation };
            resource.Status.Stream ??= new();
            resource.Status.Stream.AckedOffset = offset;
            resource.Status.ObservedGeneration = generation;
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Commits the offset that follows a delivered event, at once or as part of a batch, as configured by the broker's <see cref="OffsetCommitOptions"/>
    /// </summary>
    /// <param name="offset">The offset to commit</param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    /// <remarks>
    /// A batched offset is only written once enough deliveries have been made, or once the configured interval has elapsed. The handler resumes from it in the
    /// meantime, so batching only affects what another broker, or this one after a restart, resumes from (see #162)
    /// </remarks>
    protected virtual async Task CommitDeliveredOffsetAsync(ulong offset)
    {
        var options = this.Options.OffsetCommits;
        if (!options.IsBatched)
        {
            await this.CommitOffsetAsync(offset).ConfigureAwait(false);
            return;
        }
        bool flush, schedule;
        lock (this._syncLock)
        {
            this._uncommittedOffset = offset;
            this._uncommittedOffsetGeneration = this.Subscription.Metadata.Generation;
            this._uncommittedDeliveries++;
            this.CommittedOffset = offset;
            this.CommittedOffsetGeneration = this.Subscription.Metadata.Generation;
            flush = this._uncommittedDeliveries >= options.BatchSize;
            schedule = !flush && this._uncommittedDeliveries == 1;
        }
        if (flush) await this.FlushOffsetAsync().ConfigureAwait(false);
        else if (schedule) this.ScheduleOffsetFlush(options.Interval);
    }

    /// <summary>
    /// Writes the offset of the last delivered event, if it has not been committed yet
    /// </summary>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    protected virtual async Task FlushOffsetAsync()
    {
        if (!this.Options.OffsetCommits.IsBatched) return;
        await this._commitLock.WaitAsync(this.CancellationTokenSource.Token).ConfigureAwait(false);
        try
        {
            ulong? offset, generation;
            lock (this._syncLock)
            {
                offset = this._uncommittedOffset;
                generation = this._uncommittedOffsetGeneration;
                this.DiscardUncommittedOffset();
            }
            // an offset delivered before the user changed the subscription's offset is obsolete
            if (!offset.HasValue || generation != this.Subscription.Metadata.Generation) return;
            try
            {
                await this.CommitOffsetAsync(offset).ConfigureAwait(false);
            }
            catch
            {
                // the offset is kept for the next attempt, unless a more recent one has been delivered in the meantime
                lock (this._syncLock)
                {
                    if (!this._uncommittedOffset.HasValue)
                    {
                        this._uncommittedOffset = offset;
                        this._uncommittedOffsetGeneration = generation;
                        this._uncommittedDeliveries = 1;
                    }
                }
                throw;
            }
        }
        finally { this._commitLock.Release(); }
    }

    /// <summary>
    /// Discards the offset that has been delivered but not committed yet, if any. Must be called while holding the sync lock
    /// </summary>
    void DiscardUncommittedOffset()
    {
        this._uncommittedOffset = null;
        this._uncommittedOffsetGeneration = null;
        this._uncommittedDeliveries = 0;
    }

    /// <summary>
    /// Schedules the writing of the offset of the last delivered event
    /// </summary>
    /// <param name="delay">The delay after which to write the offset</param>
    protected virtual void ScheduleOffsetFlush(TimeSpan delay)
    {
        var cancellationToken = this.CancellationTokenSource.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                await this.FlushOffsetAsync().ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException) { }
            catch (Exception ex)
            {
                this.Logger.LogWarning(ex, "Failed to commit the offset of subscription '{subscription}': it will be committed with the next delivery", this.Subscription.GetQualifiedName());
                lock (this._syncLock) if (this._uncommittedOffset.HasValue && this._uncommittedDeliveries == 1) this.ScheduleOffsetFlush(delay);
            }
        }, CancellationToken.None);
    }

    /// <summary>
    /// Cancels the subscription's ongoing synchronization loop, if any
    /// </summary>
    public async Task CancelSynchronizationLoopAsync()
    {
        if (this._disposed || this.CancellationTokenSource == null) return;
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
    /// Patches the subscription's status with the specified change
    /// </summary>
    /// <param name="update">An action that applies the change to a copy of the subscription</param>
    /// <returns>A boolean indicating whether or not the status has been patched, which it is not if the change made no difference</returns>
    /// <remarks>
    /// The patch is computed against the latest known version of the subscription, which is the result of the last patch or the last update reported by the
    /// resource watch, and only applies if the subscription is still at that version. Other writers, such as the gateway's acknowledgement endpoint, the dashboard
    /// or another broker replica, patch the same status: when one of them has changed it in the meantime, the subscription is read again and the change is applied
    /// to its current state, so that no update overwrites another (see #161)
    /// </remarks>
    /// <exception cref="ProblemDetailsException">The status could not be patched, for example because it kept changing concurrently</exception>
    protected virtual Task<bool> PatchStatusAsync(Action<Subscription> update) => this.StatusReporter.PatchAsync(update, this.CancellationTokenSource.Token);

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
        await this.StatusReporter.SetPhaseAsync(phase, this.CancellationTokenSource.Token).ConfigureAwait(false);
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
        await this.StatusReporter.SetSubscriberStateAsync(this.Subscription, state, reason, this.CancellationTokenSource.Token).ConfigureAwait(false);
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
            if (this.Subscription.Spec.Suspended)
            {
                await this.InitLock.WaitAsync(this.CancellationTokenSource.Token).ConfigureAwait(false);
                try
                {
                    if (this.LastRequestedOffset == offset) return;
                    this.LastRequestedOffset = offset;
                }
                finally { this.InitLock.Release(); }
                // the change is applied at once, rather than when the stream is initialized again: resuming takes the acknowledged offset as current (see #214).
                // As for a faulted subscription, changing the offset also clears its fault
                await this.ResetOffsetAsync().ConfigureAwait(false);
                if (this.Subscription.Status?.Stream?.Fault != null) await this.PatchStatusAsync(resource =>
                {
                    resource.Status ??= new();
                    resource.Status.Stream ??= new();
                    resource.Status.Stream.Fault = null;
                }).ConfigureAwait(false);
                return;
            }
            if (this.Subscription.Status?.Stream?.Fault != null)
            {
                this.LastRequestedOffset = offset;
                await this.PatchStatusAsync(resource =>
                {
                    resource.Status ??= new() { ObservedGeneration = this.Subscription.Metadata.Generation };
                    resource.Status.Stream ??= new();
                    resource.Status.Stream.Fault = null;
                }).ConfigureAwait(false);
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
    /// Handles the subscription switching between delivering one live record at a time, and delivering in batches or in parallel
    /// </summary>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    protected virtual async Task OnDeliveryModeChangedAsync()
    {
        try
        {
            await this.CancelSynchronizationLoopAsync().ConfigureAwait(false);
            await this.FlushOffsetAsync().ConfigureAwait(false);
            await this.ObserveGenerationAsync().ConfigureAwait(false);
            await this.InitializeCloudEventStreamAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is OperationCanceledException or TaskCanceledException || (ex is RpcException rpcException && rpcException.StatusCode == StatusCode.Cancelled)) { }
        catch (Exception ex) { await this.OnSubscriptionErrorAsync(ex).ConfigureAwait(false); }
    }

    /// <summary>
    /// Handles the suspension of the subscription, or its resumption
    /// </summary>
    /// <param name="suspended">A boolean indicating whether or not the subscription is suspended</param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    /// <remarks>
    /// A suspended subscription stops streaming, as a faulted one does, but keeps its broker and its lease: the offset of whatever has been delivered is committed, and
    /// delivery resumes from it. Suspending and resuming change the subscription's spec, and therefore its generation, which invalidates the acknowledged offset: the
    /// new generation is reported as observed, unless the offset has been changed as well, in which case the change applies as usual (see #214)
    /// </remarks>
    protected virtual async Task OnSubscriptionSuspensionChangedAsync(bool suspended)
    {
        try
        {
            if (suspended)
            {
                await this.CancelSynchronizationLoopAsync().ConfigureAwait(false);
                await this.FlushOffsetAsync().ConfigureAwait(false);
                await this.CommitReachedPositionAsync().ConfigureAwait(false);
            }
            await this.ObserveGenerationAsync(status => status.Phase = suspended ? SubscriptionStatusPhase.Suspended : SubscriptionStatusPhase.Active).ConfigureAwait(false);
            if (suspended) return;
            await this.InitializeCloudEventStreamAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is OperationCanceledException or TaskCanceledException || (ex is RpcException rpcException && rpcException.StatusCode == StatusCode.Cancelled)) { }
        catch (Exception ex) { await this.OnSubscriptionErrorAsync(ex).ConfigureAwait(false); }
    }

    /// <summary>
    /// Reports the subscription's current generation as observed, unless its offset has changed, so that its acknowledged offset remains the position to resume from
    /// </summary>
    /// <param name="update">An action that applies further changes to the status, if any</param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    /// <remarks>
    /// Changing the subscription's spec, e.g. to suspend it or to deliver in batches, changes its generation, which invalidates its acknowledged offset: the stream would
    /// otherwise restart at the offset requested by the spec. The stream is initialized again at once, from the handler's view of the subscription, which the watch
    /// has yet to update: the observed generation is therefore set on it too (see #212 and #214)
    /// </remarks>
    protected virtual async Task ObserveGenerationAsync(Action<SubscriptionStatus>? update = null)
    {
        var subscription = this.Subscription;
        var generation = subscription.Metadata.Generation;
        var offsetUnchanged = subscription.Spec.Stream?.Offset == this.LastRequestedOffset;
        await this.PatchStatusAsync(resource =>
        {
            resource.Status ??= new();
            update?.Invoke(resource.Status);
            if (offsetUnchanged && resource.Status.ObservedGeneration < generation) resource.Status.ObservedGeneration = generation;
        }).ConfigureAwait(false);
        if (offsetUnchanged && subscription.Status?.ObservedGeneration < generation && ReferenceEquals(this.Subscription, subscription))
        {
            var observed = subscription.Clone()!;
            observed.Status!.ObservedGeneration = generation;
            this.Subscription = observed;
        }
    }

    /// <summary>
    /// Commits the position reached by a subscription that tracks the end of the stream, and has not committed any offset yet
    /// </summary>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    /// <remarks>
    /// Such a subscription has no position to resume from other than the end of the stream, and would skip whatever is published while it is suspended (see #214)
    /// </remarks>
    protected virtual async Task CommitReachedPositionAsync()
    {
        if (this.Subscription.Spec.Stream == null || this.CommittedOffset.HasValue || this.StatusReporter.Latest.Status?.Stream?.AckedOffset.HasValue == true) return;
        // a subscription that starts at an earlier offset resumes from it
        if (this.Subscription.GetOffset() != StreamPosition.EndOfStream) return;
        ulong? position;
        // a delivery being retried has not been handled: the position is unknown, and the subscription resumes at the end of the stream, as before
        lock (this._syncLock) position = this.AwaitingAcknowledgement ? this.PendingAcknowledgementOffset : this._nextOffset ?? (this.SubscriptionOutOfSync ? null : this.StreamOffset);
        if (!position.HasValue) return;
        await this._commitLock.WaitAsync(this.CancellationTokenSource.Token).ConfigureAwait(false);
        try
        {
            if (!this.CommittedOffset.HasValue) await this.CommitOffsetAsync(position.Value).ConfigureAwait(false);
        }
        finally { this._commitLock.Release(); }
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
            // a subscription delivering in batches or in parallel reads what it delivers from the store: live records only tell it there is more to read (see #212)
            if (this.IsPumped)
            {
                this.SignalPump();
                return;
            }
            // an unreachable subscriber is deliberately not a reason to drop the event: while a delivery is being retried the subscription is out of sync,
            // and a persisted 'unreachable' state (i.e. after a broker restart) would otherwise drop every live event forever
            if (this.Subscription.Status?.Stream?.Fault != null || this.Subscription.Spec.Suspended || outOfSync) return;
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
    /// Recording the fault is retried with backoff, and abandoned when the subscription no longer exists. This method never throws, since most of its callers are
    /// fire-and-forget tasks or reactive callbacks, where an exception would go unobserved (see #159)
    /// </remarks>
    protected virtual async Task OnSubscriptionErrorAsync(Exception ex)
    {
        if (this._disposed) return;
        // a canceled stream is the consequence of stopping it, for example to fault the subscription, not a fault of its own: recording it would replace the actual fault
        // with 'The operation was canceled'
        if ((ex is OperationCanceledException && ex.InnerException is not TimeoutException) || (ex is RpcException cancelled && cancelled.StatusCode == StatusCode.Cancelled))
        {
            this.Logger.LogDebug(ex, "The stream of subscription '{subscription}' has been canceled", this.Subscription.GetQualifiedName());
            return;
        }
        try { this.StreamInitializationCancellationTokenSource?.Cancel(); }
        catch (ObjectDisposedException) { }
        this.Logger.LogError(ex, "An error occurred while streaming cloud events for subscription '{subscription}'", this.Subscription.GetQualifiedName());
        if (this.Subscription.Spec.Stream == null) return;
        // the offset to resume from must reflect every event delivered before the fault
        try { await this.FlushOffsetAsync().ConfigureAwait(false); }
        catch (Exception flushException) { this.Logger.LogWarning(flushException, "Failed to commit the offset of subscription '{subscription}' before faulting it", this.Subscription.GetQualifiedName()); }
        for (var attempt = 1; attempt <= MaxFaultReportingAttempts && !this._disposed; attempt++)
        {
            try
            {
                var fault = ex.ToProblemDetails();
                await this.PatchStatusAsync(resource =>
                {
                    resource.Status ??= new() { ObservedGeneration = this.Subscription.Metadata.Generation };
                    resource.Status.Stream ??= new();
                    // the first fault is the one that explains why the subscription stopped: the errors it causes in turn must not replace it. Resuming clears it
                    resource.Status.Stream.Fault ??= fault;
                }).ConfigureAwait(false);
                return;
            }
            catch (Exception inner) when (inner is OperationCanceledException or TaskCanceledException or ObjectDisposedException || (inner is RpcException rpcException && rpcException.StatusCode == StatusCode.Cancelled))
            {
                return;
            }
            catch (ProblemDetailsException inner) when (inner.Problem.Status == (int)HttpStatusCode.NotFound)
            {
                this.Logger.LogDebug("Abandoned recording the fault of subscription '{subscription}', which no longer exists", this.Subscription.GetQualifiedName());
                return;
            }
            catch (Exception inner)
            {
                if (attempt == MaxFaultReportingAttempts)
                {
                    this.Logger.LogError(inner, "Failed to record the fault of subscription '{subscription}' after {attempts} attempts", this.Subscription.GetQualifiedName(), attempt);
                    return;
                }
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt - 1));
                this.Logger.LogWarning(inner, "Failed to record the fault of subscription '{subscription}' (attempt {attempt}/{maxAttempts}). Retrying in {delay}...", this.Subscription.GetQualifiedName(), attempt, MaxFaultReportingAttempts, delay);
                try { await Task.Delay(delay, this.CancellationTokenSource.Token).ConfigureAwait(false); }
                catch (Exception delayException) when (delayException is OperationCanceledException or ObjectDisposedException) { return; }
            }
        }
    }

    /// <summary>
    /// Disposes of the <see cref="SubscriptionHandler"/>
    /// </summary>
    /// <param name="disposing">A boolean indicating whether or not the <see cref="SubscriptionHandler"/> is being disposed of</param>
    /// <remarks>
    /// Disposing of a <see cref="System.Threading.CancellationTokenSource"/> does not signal its token, so every token source is canceled before being disposed of.
    /// This ends the reactive subscriptions bound to the handler's token, the synchronization loop and the acknowledgement timer, so that a disposed handler never
    /// reacts to updates nor dispatches anything (see #153)
    /// </remarks>
    protected virtual async ValueTask DisposeAsync(bool disposing)
    {
        if (this._disposed) return;
        this._disposed = true;
        if (!disposing) return;
        this._applicationStoppingRegistration.Dispose();
        // whatever has been delivered is committed before stopping, so that the next handler does not redeliver it
        if (this.CancellationTokenSource != null && !this.CancellationTokenSource.IsCancellationRequested)
        {
            try { await this.FlushOffsetAsync().WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false); }
            catch (Exception ex) { this.Logger.LogWarning(ex, "Failed to commit the offset of subscription '{subscription}' before stopping: the events delivered since the last commit will be redelivered", this.Subscription.GetQualifiedName()); }
        }
        // the handler's token is canceled first: in-flight deliveries, retries and backoffs are bound to it, and would otherwise delay the disposal
        try { this.CancellationTokenSource?.Cancel(); }
        catch (ObjectDisposedException) { }
        Task? streamSynchronizationTask;
        CancellationTokenSource? streamInitializationCancellationTokenSource;
        IDisposable? subscriptionHandle;
        await this.InitLock.WaitAsync(CancellationToken.None).ConfigureAwait(false);
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
        try { streamInitializationCancellationTokenSource?.Cancel(); }
        catch (ObjectDisposedException) { }
        if (streamSynchronizationTask != null)
        {
            try { await streamSynchronizationTask.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false); }
            catch (Exception) { /* the loop has been canceled: whatever it ended with is irrelevant to a disposed handler */ }
        }
        subscriptionHandle?.Dispose();
        this._lagRegistration?.Dispose();
        this.Acknowledgements.Dispose();
        streamInitializationCancellationTokenSource?.Dispose();
        this.CancellationTokenSource?.Dispose();
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await this.DisposeAsync(disposing: true);
        GC.SuppressFinalize(this);
    }

}
