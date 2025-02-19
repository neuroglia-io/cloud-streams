﻿// Copyright © 2024-Present The Cloud Streams Authors
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
using Polly.CircuitBreaker;
using Polly;
using System.Text.RegularExpressions;
using System.Reactive.Threading.Tasks;
using System.Numerics;

namespace CloudStreams.Broker.Application.Services;

/// <summary>
/// Represents a service used to handle a <see cref="Core.Resources.Subscription"/>
/// </summary>
public class SubscriptionHandler
{

    bool _disposed;

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
    /// <param name="subscription">The <see cref="Core.Resources.Subscription"/> to dispatch <see cref="CloudEvent"/>s to</param>
    public SubscriptionHandler(ILoggerFactory loggerFactory, IHostApplicationLifetime hostApplicationLifetime, IJsonSerializer serializer, ICloudEventStore cloudEventStore, IResourceRepository resourceRepository, IResourceController<Subscription> subscriptionController,
        IResourceMonitor<Core.Resources.Broker> broker, IExpressionEvaluator expressionEvaluator, IEnumerable<IValidator<CloudEvent>> cloudEventValidators, HttpClient httpClient, Subscription subscription)
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
    protected IObservable<CloudEventRecord> CloudEventStream { get; private set; } = null!;

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
    /// Initializes the <see cref="SubscriptionHandler"/>
    /// </summary>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    public virtual async Task InitializeAsync(CancellationToken cancellationToken)
    {
        this.CancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
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
        if (this.Subscription.Status?.Stream?.Fault != null) return;
        await this.InitLock.WaitAsync(this.CancellationTokenSource.Token).ConfigureAwait(false);
        try
        {
            if (this.Subscription.Metadata.Generation > this.Subscription.Status?.ObservedGeneration
                && this.Subscription.Spec.Stream?.Offset != (long?)this.Subscription.Status?.Stream?.AckedOffset)
            {
                await this.CommitOffsetAsync(null).ConfigureAwait(false);
            }
            this.SubscriptionHandle?.Dispose();
            this.SubscriptionHandle = null;
            this.StreamInitializationCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(this.CancellationTokenSource.Token);
            try
            {
                var offset = this.Subscription.GetOffset();
                this.Logger.LogDebug("Initializing the cloud event stream of subscription '{subscription}' at offset '{offset}'", this.Subscription, offset);
                if (this.Subscription.Spec.Partition == null)
                {
                    while (true && this.StreamInitializationCancellationTokenSource != null && !this.StreamInitializationCancellationTokenSource.IsCancellationRequested)
                    {
                        try
                        {
                            this.CloudEventStream = await this.EventStore.ObserveAsync(offset, this.StreamInitializationCancellationTokenSource.Token).ConfigureAwait(false);
                            this.StreamOffset = (await this.EventStore.GetStreamMetadataAsync(this.StreamInitializationCancellationTokenSource.Token).ConfigureAwait(false)).Length;
                            if (offset >= 0 && (ulong)offset == this.StreamOffset) offset = -1;
                            break;
                        }
                        catch (StreamNotFoundException)
                        {
                            var delay = 5000;
                            this.Logger.LogDebug("Failed to observe the cloud event stream because the first cloud event is yet to be published. Retrying in {delay} milliseconds...", delay);
                            await Task.Delay(delay).ConfigureAwait(false);
                        }
                    }
                }
                else
                {
                    while (true && this.StreamInitializationCancellationTokenSource != null && !this.StreamInitializationCancellationTokenSource.IsCancellationRequested)
                    {
                        try
                        {
                            this.CloudEventStream = await this.EventStore.ObservePartitionAsync(this.Subscription.Spec.Partition, offset, this.StreamInitializationCancellationTokenSource.Token).ConfigureAwait(false);
                            this.StreamOffset = (await this.EventStore.GetPartitionMetadataAsync(this.Subscription.Spec.Partition, this.StreamInitializationCancellationTokenSource.Token).ConfigureAwait(false)).Length;
                            if (offset >= 0 && (ulong)offset == this.StreamOffset) offset = -1;
                            break;
                        }
                        catch (StreamNotFoundException)
                        {
                            var delay = 5000;
                            this.Logger.LogDebug("Failed to observe the cloud event stream because the first cloud event is yet to be published. Retrying in {delay} milliseconds...", delay);
                            await Task.Delay(delay).ConfigureAwait(false);
                        }
                    }
                }
                this.SubscriptionHandle = this.CloudEventStream.ToAsyncEnumerable().WhereAwait(this.FiltersAsync).ToObservable().SubscribeAsync(this.OnCloudEventAsync, onErrorAsync: this.OnSubscriptionErrorAsync, null);
                if (this.Subscription.Status?.ObservedGeneration == null || (offset != StreamPosition.EndOfStream && (ulong)offset < this.StreamOffset)) _ = this.CatchUpAsync().ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is OperationCanceledException or TaskCanceledException || (ex is RpcException rpcException && rpcException.StatusCode == StatusCode.Cancelled)) { }
            catch (Exception ex) { await this.OnSubscriptionErrorAsync(ex); }
        }
        finally { this.InitLock.Release(); }
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
        try
        {
            await this.SetSubscriberStateAsync(SubscriberState.Unreachable, statusReason).ConfigureAwait(false);
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
        try
        {
            this.SubscriptionOutOfSync = true;
            this.StreamSynchronizationTaskCompletionSource ??= new();
            var currentOffset = this.Subscription.GetOffset();
            if (currentOffset == StreamPosition.EndOfStream) currentOffset = this.Subscription.Spec.Partition == null ?
                (long)(await this.EventStore.ReadOneAsync(StreamReadDirection.Backwards, StreamPosition.EndOfStream, this.StreamInitializationCancellationTokenSource!.Token).ConfigureAwait(false))!.Sequence
                : (long)(await this.EventStore.ReadPartitionAsync(this.Subscription.Spec.Partition, StreamReadDirection.Backwards, StreamPosition.EndOfStream, 1, this.StreamInitializationCancellationTokenSource!.Token).SingleAsync(this.StreamInitializationCancellationTokenSource!.Token).ConfigureAwait(false))!.Sequence;
            do
            {
                var record = this.Subscription.Spec.Partition == null ?
                    await this.EventStore.ReadOneAsync(StreamReadDirection.Forwards, currentOffset!, this.StreamInitializationCancellationTokenSource!.Token).ConfigureAwait(false)
                    : await this.EventStore.ReadPartitionAsync(this.Subscription.Spec.Partition, StreamReadDirection.Forwards, currentOffset, 1, this.StreamInitializationCancellationTokenSource!.Token).SingleOrDefaultAsync(this.StreamInitializationCancellationTokenSource!.Token).ConfigureAwait(false);
                if (record == null)
                {
                    await Task.Delay(50);
                    continue;
                }
                await this.DispatchAsync(record, true, false).ConfigureAwait(false);
                currentOffset++;
            }
            while (this.StreamInitializationCancellationTokenSource != null && !this.StreamInitializationCancellationTokenSource.Token.IsCancellationRequested && (ulong)currentOffset <= this.StreamOffset);
            this.SubscriptionOutOfSync = false;
        }
        catch (Exception ex) when (ex is ObjectDisposedException || ex is TaskCanceledException || ex is OperationCanceledException || (ex is RpcException rpcException && rpcException.StatusCode == StatusCode.Cancelled)) { }
        finally { this.StreamSynchronizationTaskCompletionSource?.SetResult(); }
    }

    /// <summary>
    /// Commits the specified offset
    /// </summary>
    /// <param name="offset">The <see cref="Subscription"/>'s offset to commit</param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    protected virtual async Task CommitOffsetAsync(ulong? offset)
    {
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
        this.StreamInitializationCancellationTokenSource?.Cancel();
        if (this.StreamSynchronizationTaskCompletionSource != null)
        {
            try
            {
                await this.StreamSynchronizationTaskCompletionSource.Task.ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is OperationCanceledException or TaskCanceledException || (ex is RpcException rpcException && rpcException.StatusCode == StatusCode.Cancelled)) { }
        }
        this.StreamInitializationCancellationTokenSource?.Dispose();
        this.StreamInitializationCancellationTokenSource = null;
        this.StreamSynchronizationTaskCompletionSource = null;
        this.SubscriptionHandle?.Dispose();
        this.SubscriptionHandle = null;
    }

    /// <summary>
    /// Sets the <see cref="Core.Resources.Subscription"/>'s status phase
    /// </summary>
    /// <param name="phase">The <see cref="Core.Resources.Subscription"/>'s status phase</param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    protected virtual async Task SetStatusPhaseAsync(SubscriptionStatusPhase phase)
    {
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
                var resource = this.Subscription.Clone()!;
                if (resource.Status == null) resource.Status = new() { ObservedGeneration = this.Subscription.Metadata.Generation };
                if (resource.Status.Stream == null) resource.Status.Stream = new();
                resource.Status.Stream.Fault = null;
                var patch = JsonPatchUtility.CreateJsonPatchFromDiff(this.Subscription, resource);
                await this.ResourceRepository.PatchStatusAsync<Subscription>(new Patch(PatchType.JsonPatch, patch), resource.GetName(), resource.GetNamespace(), null, false, this.CancellationTokenSource.Token).ConfigureAwait(false);
                return;
            }
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
