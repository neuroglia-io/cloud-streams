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

using CloudStreams.Broker.Application.Services;
using FluentValidation;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Neuroglia.Data.Expressions.Services;
using System.Diagnostics;
using System.Reactive.Subjects;

namespace CloudStreams.UnitTests.Services;

/// <summary>
/// Assembles a <see cref="TestableSubscriptionHandler"/> and the twelve collaborators it requires
/// </summary>
/// <remarks>
/// There are two fixture modes:
/// <list type="bullet">
/// <item><b>Dispatch mode</b> (the default) suppresses the fire-and-forget stream initialization, so the handler is fully
/// deterministic. Use it for retry and subscriber-state cases.</item>
/// <item><b>Stream mode</b> (<c>initializeStream: true</c>) lets the real initialization run, because it is the only thing
/// that populates the token source <c>CatchUpAsync</c> requires. Use it for catch-up cases.</item>
/// </list>
/// </remarks>
public sealed class SubscriptionHandlerTestContext
    : IAsyncDisposable
{

    readonly CancellationTokenSource _cancellationTokenSource = new();
    bool _disposed;

    SubscriptionHandlerTestContext(Subscription subscription, Core.Resources.Broker broker, TimeSpan? httpTimeout)
    {
        this.Subscription = subscription;
        this.Http = new StubHttpMessageHandler();
        this.HttpClient = new HttpClient(this.Http);
        if (httpTimeout.HasValue) this.HttpClient.Timeout = httpTimeout.Value;
        this.EventStore = new FakeCloudEventStore();
        this.Metrics = new RecordingBrokerMetrics();
        this.Repository = new ResourcePatchRecorder(subscription);
        this.SubscriptionEvents = new Subject<IResourceWatchEvent<Subscription>>();
        this.BrokerEvents = new Subject<IResourceWatchEvent<Core.Resources.Broker>>();

        var subscriptionController = Substitute.For<IResourceController<Subscription>>();
        subscriptionController.Subscribe(Arg.Any<IObserver<IResourceWatchEvent<Subscription>>>()).Returns(call => this.SubscriptionEvents.Subscribe(call.Arg<IObserver<IResourceWatchEvent<Subscription>>>()));

        var brokerMonitor = Substitute.For<IResourceMonitor<Core.Resources.Broker>>();
        brokerMonitor.Resource.Returns(broker);
        brokerMonitor.Subscribe(Arg.Any<IObserver<IResourceWatchEvent<Core.Resources.Broker>>>()).Returns(call => this.BrokerEvents.Subscribe(call.Arg<IObserver<IResourceWatchEvent<Core.Resources.Broker>>>()));

        var lifetime = Substitute.For<IHostApplicationLifetime>();
        lifetime.ApplicationStopping.Returns(CancellationToken.None);
        lifetime.ApplicationStarted.Returns(CancellationToken.None);
        lifetime.ApplicationStopped.Returns(CancellationToken.None);

        this.Handler = new TestableSubscriptionHandler(
            NullLoggerFactory.Instance,
            lifetime,
            Neuroglia.Serialization.Json.JsonSerializer.Default,
            this.EventStore,
            this.Repository.Repository,
            subscriptionController,
            brokerMonitor,
            Substitute.For<IExpressionEvaluator>(),
            [],
            this.HttpClient,
            this.Metrics,
            subscription);
    }

    /// <summary>
    /// Gets the <see cref="TestableSubscriptionHandler"/> under test
    /// </summary>
    public TestableSubscriptionHandler Handler { get; }

    /// <summary>
    /// Gets the <see cref="Subscription"/> the handler dispatches events for
    /// </summary>
    public Subscription Subscription { get; }

    /// <summary>
    /// Gets the stub that scripts the subscriber's responses and records its requests
    /// </summary>
    public StubHttpMessageHandler Http { get; }

    /// <summary>
    /// Gets the <see cref="System.Net.Http.HttpClient"/> the handler dispatches through
    /// </summary>
    public HttpClient HttpClient { get; }

    /// <summary>
    /// Gets the in-memory event store the handler reads from
    /// </summary>
    public FakeCloudEventStore EventStore { get; }

    /// <summary>
    /// Gets the recorder of the handler's broker metrics
    /// </summary>
    public RecordingBrokerMetrics Metrics { get; }

    /// <summary>
    /// Gets the recorder of the status patches the handler writes
    /// </summary>
    public ResourcePatchRecorder Repository { get; }

    /// <summary>
    /// Gets the <see cref="Subject{T}"/> used to push subscription resource watch events to the handler
    /// </summary>
    public Subject<IResourceWatchEvent<Subscription>> SubscriptionEvents { get; }

    /// <summary>
    /// Gets the <see cref="Subject{T}"/> used to push broker resource watch events to the handler
    /// </summary>
    public Subject<IResourceWatchEvent<Core.Resources.Broker>> BrokerEvents { get; }

    /// <summary>
    /// Creates and initializes a new <see cref="SubscriptionHandlerTestContext"/>
    /// </summary>
    /// <param name="retryPolicy">The subscriber's <see cref="HttpClientRetryPolicy"/>, if any</param>
    /// <param name="brokerRetryPolicy">The broker's default <see cref="HttpClientRetryPolicy"/>, if any</param>
    /// <param name="httpTimeout">The <see cref="System.Net.Http.HttpClient"/>'s timeout, if any</param>
    /// <param name="initializeStream">A boolean indicating whether to let the cloud event stream initialize (stream mode)</param>
    /// <param name="offset">The offset the subscription starts streaming at, if any</param>
    /// <returns>A new, initialized <see cref="SubscriptionHandlerTestContext"/></returns>
    public static async Task<SubscriptionHandlerTestContext> CreateAsync(HttpClientRetryPolicy? retryPolicy = null, HttpClientRetryPolicy? brokerRetryPolicy = null, TimeSpan? httpTimeout = null, bool initializeStream = false, long? offset = 0, CloudEventRecord[]? seed = null)
    {
        var context = new SubscriptionHandlerTestContext(SubscriptionFactory.Subscription(retryPolicy, offset), SubscriptionFactory.Broker(brokerRetryPolicy), httpTimeout);
        //seed before initializing: the handler snapshots the stream length during initialization, and the catch-up loop
        //bounds itself against that snapshot
        if (seed != null) context.EventStore.With(seed);
        context.Handler.SkipStreamInitialization = !initializeStream;
        await context.Handler.InitializeAsync(context._cancellationTokenSource.Token).ConfigureAwait(false);
        await context.WaitUntilStreamInitializedAsync().ConfigureAwait(false);
        return context;
    }

    /// <summary>
    /// Waits until the subscriber has received at least the specified number of requests
    /// </summary>
    /// <param name="count">The number of requests to wait for</param>
    /// <param name="timeout">The maximum amount of time to wait for. Defaults to 5 seconds</param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    public async Task WaitForRequestsAsync(int count, TimeSpan? timeout = null)
    {
        var stopwatch = Stopwatch.StartNew();
        var limit = timeout ?? TimeSpan.FromSeconds(5);
        while (this.Http.RequestCount < count)
        {
            if (stopwatch.Elapsed > limit) throw new TimeoutException($"The subscriber received {this.Http.RequestCount} of the {count} expected requests within '{limit}'.");
            await Task.Delay(10).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Waits until the handler has initialized its cloud event stream at least the specified number of times
    /// </summary>
    /// <param name="count">The number of initializations to wait for</param>
    /// <param name="timeout">The maximum amount of time to wait for. Defaults to 5 seconds</param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    public async Task WaitForStreamInitializationsAsync(int count, TimeSpan? timeout = null)
    {
        var stopwatch = Stopwatch.StartNew();
        var limit = timeout ?? TimeSpan.FromSeconds(5);
        while (this.Handler.StreamInitializationCount < count)
        {
            if (stopwatch.Elapsed > limit) throw new TimeoutException($"The cloud event stream was initialized {this.Handler.StreamInitializationCount} of the {count} expected times within '{limit}'.");
            await Task.Delay(10).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Pushes an update of the managed <see cref="Subscription"/> to the handler, as the resource watch would
    /// </summary>
    /// <param name="subscription">The updated <see cref="Subscription"/></param>
    public void PushSubscriptionUpdate(Subscription subscription) => this.SubscriptionEvents.OnNext(new ResourceWatchEvent<Subscription>(ResourceWatchEventType.Updated, subscription));

    /// <summary>
    /// Waits until the handler's fire-and-forget stream initialization has run to completion
    /// </summary>
    /// <remarks>
    /// <c>InitializeAsync</c> starts the stream initialization without awaiting it, so returning from it says nothing about
    /// whether the stream is ready. Waiting on an intermediate flag is not enough either: the token source is assigned early
    /// in initialization, well before the stream is observed and the subscription handle is installed.
    /// </remarks>
    /// <param name="timeout">The maximum amount of time to wait for. Defaults to 10 seconds</param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    public async Task WaitUntilStreamInitializedAsync(TimeSpan? timeout = null)
    {
        try { await this.Handler.StreamInitialized.WaitAsync(timeout ?? TimeSpan.FromSeconds(10)).ConfigureAwait(false); }
        catch (TimeoutException) { throw new TimeoutException($"The cloud event stream did not initialize within '{timeout ?? TimeSpan.FromSeconds(10)}'."); }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (this._disposed) return;
        this._disposed = true;
        //drain before cancelling. Disposing the handler disposes the CancellationTokenSource that in-flight initialization
        //and catch-up still dereference, and neither of their catch filters covers ObjectDisposedException, so tearing down
        //underneath live work leaks a faulted task into whichever test runs next
        try { await this.Handler.StreamInitialized.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false); } catch { }
        try { await this.Handler.CancelSynchronizationLoopAsync().ConfigureAwait(false); } catch { }
        //cancellation and disposal faults raised against fakes belong to the teardown, not to the behaviour under test
        try { await this._cancellationTokenSource.CancelAsync().ConfigureAwait(false); } catch { }
        try { await this.Handler.DisposeAsync().ConfigureAwait(false); } catch { }
        this._cancellationTokenSource.Dispose();
        this.HttpClient.Dispose();
        this.EventStore.Dispose();
        this.SubscriptionEvents.Dispose();
        this.BrokerEvents.Dispose();
    }

}
