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
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Neuroglia.Data.Expressions.Services;
using Neuroglia.Serialization;

namespace CloudStreams.UnitTests.Services;

/// <summary>
/// Assembles a <see cref="SubscriptionHandler"/> and the fakes it depends on, so a test can focus on dispatch behaviour
/// </summary>
public sealed class BrokerTestContext
    : IAsyncDisposable
{

    BrokerTestContext(TestSubscriptionHandler handler, StubHttpMessageHandler http, HttpClient httpClient, StatusPatchRecorder patches, RecordingBrokerMetrics metrics,
        InMemoryCloudEventStore eventStore, Subject<IResourceWatchEvent<Subscription>> subscriptionEvents, Subscription subscription, Core.Resources.Broker broker)
    {
        this.Handler = handler;
        this.Http = http;
        this.HttpClient = httpClient;
        this.Patches = patches;
        this.Metrics = metrics;
        this.EventStore = eventStore;
        this.SubscriptionEvents = subscriptionEvents;
        this.Subscription = subscription;
        this.Broker = broker;
    }

    /// <summary>
    /// Gets the <see cref="SubscriptionHandler"/> under test, with its protected surface exposed
    /// </summary>
    public TestSubscriptionHandler Handler { get; }

    /// <summary>
    /// Gets the handler used to script subscriber responses and count dispatch attempts
    /// </summary>
    public StubHttpMessageHandler Http { get; }

    /// <summary>
    /// Gets the <see cref="System.Net.Http.HttpClient"/> handed to the handler
    /// </summary>
    public HttpClient HttpClient { get; }

    /// <summary>
    /// Gets the status patches the handler has applied
    /// </summary>
    public StatusPatchRecorder Patches { get; }

    /// <summary>
    /// Gets the recorded broker metrics
    /// </summary>
    public RecordingBrokerMetrics Metrics { get; }

    /// <summary>
    /// Gets the in-memory <see cref="ICloudEventStore"/> backing the handler
    /// </summary>
    public InMemoryCloudEventStore EventStore { get; }

    /// <summary>
    /// Gets the subject used to simulate the resource watch that feeds subscription updates back into the handler
    /// </summary>
    public Subject<IResourceWatchEvent<Subscription>> SubscriptionEvents { get; }

    /// <summary>
    /// Gets the <see cref="Core.Resources.Subscription"/> the handler was created with
    /// </summary>
    public Subscription Subscription { get; }

    /// <summary>
    /// Gets the <see cref="Core.Resources.Broker"/> the handler was created with
    /// </summary>
    public Core.Resources.Broker Broker { get; }

    /// <summary>
    /// Pushes a subscription update through the simulated resource watch, the way the real control plane does after a status patch
    /// </summary>
    public void PublishSubscriptionUpdate(Subscription subscription) => this.SubscriptionEvents.OnNext(new ResourceWatchEvent<Subscription>(ResourceWatchEventType.Updated, subscription));

    /// <summary>
    /// Creates a new <see cref="BrokerTestContext"/> and initializes its handler
    /// </summary>
    /// <param name="http">The handler used to script subscriber responses. Defaults to always responding 200 OK</param>
    /// <param name="configureSubscription">An action used to configure the subscription under test</param>
    /// <param name="configureBroker">An action used to configure the broker the subscription belongs to</param>
    /// <param name="streamLength">The amount of records to seed the in-memory event store with</param>
    /// <param name="configureEventStore">An action used to configure the in-memory event store before initialization</param>
    /// <param name="waitForStreamInitialization">A boolean indicating whether to wait for the stream to be initialized. Set to false to test an initialization that is expected to fail</param>
    /// <remarks>
    /// The handler's <see cref="CancellationTokenSource"/> is only assigned by <c>InitializeAsync</c>, and every dispatch dereferences it, so initialization is not optional.
    /// The subscription defaults to an observed generation and an end-of-stream offset so initialization does not trigger a catch-up; tests that want one opt in explicitly,
    /// and must then reset the recorders themselves once it has settled, because catch-up is started without being awaited.
    /// Everything initialization records is discarded, so a test starts from a clean slate.
    /// </remarks>
    public static async Task<BrokerTestContext> CreateAsync(StubHttpMessageHandler? http = null, Action<Subscription>? configureSubscription = null, Action<Core.Resources.Broker>? configureBroker = null, int streamLength = 0,
        Action<InMemoryCloudEventStore>? configureEventStore = null, bool waitForStreamInitialization = true)
    {
        http ??= StubHttpMessageHandler.AlwaysResponds(HttpStatusCode.OK);
        var httpClient = http.CreateClient();

        var subscription = new Subscription()
        {
            Metadata = new() { Name = "test-subscription", Generation = 1 },
            Spec = new()
            {
                Subscriber = new() { Uri = new("http://localhost:8888/events") },
                Stream = new() { Offset = StreamPosition.EndOfStream }
            },
            Status = new() { ObservedGeneration = 1, Phase = SubscriptionStatusPhase.Active, Stream = new(), Subscriber = new() }
        };
        configureSubscription?.Invoke(subscription);

        var broker = new Core.Resources.Broker()
        {
            Metadata = new() { Name = "test-broker", Generation = 1 },
            Spec = new() { Service = new() { Uri = new("http://localhost:8080") } }
        };
        configureBroker?.Invoke(broker);

        var patches = new StatusPatchRecorder();
        var repository = CreateRepository(patches, subscription);
        var eventStore = new InMemoryCloudEventStore().Seed(streamLength);
        configureEventStore?.Invoke(eventStore);
        var subscriptionEvents = new Subject<IResourceWatchEvent<Subscription>>();
        var subscriptionController = CreateSubscriptionController(subscriptionEvents);
        var brokerMonitor = CreateBrokerMonitor(broker);
        var metrics = new RecordingBrokerMetrics();

        var handler = new TestSubscriptionHandler(NullLoggerFactory.Instance, Substitute.For<IHostApplicationLifetime>(), Substitute.For<IJsonSerializer>(), eventStore, repository,
            subscriptionController, brokerMonitor, Substitute.For<IExpressionEvaluator>(), [], httpClient, metrics, subscription);
        var context = new BrokerTestContext(handler, http, httpClient, patches, metrics, eventStore, subscriptionEvents, subscription, broker);
        try
        {
            await handler.InitializeAsync(CancellationToken.None).ConfigureAwait(false);
            if (waitForStreamInitialization)
            {
                await handler.WaitForStreamInitializationAsync().ConfigureAwait(false);
                context.ResetRecorders();
            }
            return context;
        }
        catch
        {
            // the caller never gets a handle to dispose, so everything built above has to be torn down here
            await context.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Clears everything the recorders have captured, so a test starts from a clean slate
    /// </summary>
    /// <remarks>
    /// All three recorders are cleared together. Clearing only some of them makes counts disagree about what "clean" means, which reads as a production bug when it is not.
    /// </remarks>
    public void ResetRecorders()
    {
        this.Patches.Clear();
        this.Metrics.Reset();
        this.Http.Reset();
    }

    static IResourceRepository CreateRepository(StatusPatchRecorder patches, Subscription subscription)
    {
        var repository = Substitute.For<IResourceRepository>();
        repository.PatchSubResourceAsync(Arg.Any<Patch>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                patches.Record(call.Arg<Patch>()!);
                return Task.FromResult<IResource>(subscription);
            });
        return repository;
    }

    static IResourceController<Subscription> CreateSubscriptionController(IObservable<IResourceWatchEvent<Subscription>> source)
    {
        var controller = Substitute.For<IResourceController<Subscription>>();
        controller.Subscribe(Arg.Any<IObserver<IResourceWatchEvent<Subscription>>>()).Returns(call => source.Subscribe(call.Arg<IObserver<IResourceWatchEvent<Subscription>>>()!));
        return controller;
    }

    static IResourceMonitor<Core.Resources.Broker> CreateBrokerMonitor(Core.Resources.Broker broker)
    {
        var monitor = Substitute.For<IResourceMonitor<Core.Resources.Broker>>();
        monitor.Resource.Returns(broker);
        monitor.Subscribe(Arg.Any<IObserver<IResourceWatchEvent<Core.Resources.Broker>>>())
            .Returns(call => Observable.Never<IResourceWatchEvent<Core.Resources.Broker>>().Subscribe(call.Arg<IObserver<IResourceWatchEvent<Core.Resources.Broker>>>()!));
        return monitor;
    }

    /// <summary>
    /// Creates a <see cref="CloudEventRecord"/> at the specified sequence
    /// </summary>
    public static CloudEventRecord CreateRecord(ulong sequence, string type = "com.cloud-streams.test.v1") => new("test-stream", sequence, new(new Dictionary<string, object>()
    {
        { CloudEventAttributes.SpecVersion, CloudEventSpecVersion.V1.Version },
        { CloudEventAttributes.Id, Guid.NewGuid().ToString() },
        { CloudEventAttributes.Source, "https://cloud-streams.io/tests" },
        { CloudEventAttributes.Type, type },
        { CloudEventAttributes.Time, DateTimeOffset.UtcNow.ToString("O") }
    }), null);

    /// <inheritdoc/>
    /// <remarks>
    /// Order matters. The handler owns a cancellation token source, a semaphore, a live subscription handle and a stream pump that never completes on its own,
    /// so it is torn down first. Disposing the HTTP stack while a dispatch is still in flight would throw <see cref="ObjectDisposedException"/> inside
    /// <c>DispatchAsync</c>, which only catches <see cref="HttpRequestException"/>, and the escape would fault an unrelated test's subscription.
    /// </remarks>
    public async ValueTask DisposeAsync()
    {
        // cancels the stream initialization token and waits for any in-flight catch-up to actually stop, rather than leaving it spinning
        // against an about-to-be-disposed HTTP stack
        await this.Handler.CancelSynchronizationLoopAsync().ConfigureAwait(false);
        await this.Handler.DisposeAsync().ConfigureAwait(false);
        this.SubscriptionEvents.Dispose();
        this.EventStore.Dispose();
        this.HttpClient.Dispose();
        this.Http.Dispose();
    }

}

/// <summary>
/// Exposes the <see cref="SubscriptionHandler"/> members a test needs to drive dispatch directly, without going through the resource watch
/// </summary>
/// <inheritdoc/>
public class TestSubscriptionHandler(ILoggerFactory loggerFactory, IHostApplicationLifetime hostApplicationLifetime, IJsonSerializer serializer, ICloudEventStore cloudEventStore,
    IResourceRepository resourceRepository, IResourceController<Subscription> subscriptionController, IResourceMonitor<Core.Resources.Broker> broker, IExpressionEvaluator expressionEvaluator,
    IEnumerable<IValidator<CloudEvent>> cloudEventValidators, HttpClient httpClient, IBrokerMetrics metrics, Subscription subscription)
    : SubscriptionHandler(loggerFactory, hostApplicationLifetime, serializer, cloudEventStore, resourceRepository, subscriptionController, broker, expressionEvaluator, cloudEventValidators, httpClient, metrics, subscription)
{

    /// <summary>
    /// Gets/sets a boolean indicating whether or not the subscription is out of sync with the stream's last offset
    /// </summary>
    public bool IsOutOfSync { get => this.SubscriptionOutOfSync; set => this.SubscriptionOutOfSync = value; }

    /// <summary>
    /// Gets the <see cref="Core.Resources.Subscription"/> the handler currently sees
    /// </summary>
    public Subscription CurrentSubscription => this.Subscription;

    /// <summary>
    /// Waits for the stream initialization <c>InitializeAsync</c> kicks off without awaiting, so a test does not race it
    /// </summary>
    /// <exception cref="TimeoutException">The stream was never initialized within the timeout</exception>
    /// <remarks>
    /// Throwing rather than returning matters: initialization short-circuits on a pre-existing stream fault and on the canceled, superseded and failed outcomes,
    /// none of which assign the stream. Returning quietly would hand the test a handler that was never initialized, and the real cause would surface later as an
    /// unrelated assertion failure.
    /// </remarks>
    public async Task WaitForStreamInitializationAsync(TimeSpan? timeout = null)
    {
        var effectiveTimeout = timeout ?? TimeSpan.FromSeconds(5);
        var deadline = DateTimeOffset.UtcNow + effectiveTimeout;
        while (this.CloudEventStream == null)
        {
            if (DateTimeOffset.UtcNow >= deadline) throw new TimeoutException($"The cloud event stream of subscription '{this.Subscription.GetQualifiedName()}' was not initialized within {effectiveTimeout}. Initialization short-circuits when the subscription carries a stream fault, or when the attempt is canceled, superseded or failed.");
            await Task.Delay(10).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Dispatches the specified <see cref="CloudEventRecord"/> to the configured subscriber
    /// </summary>
    public Task DispatchRecordAsync(CloudEventRecord e, bool retryOnError = true, bool catchUpWhenAvailable = false) => this.DispatchAsync(e, retryOnError, catchUpWhenAvailable);

    /// <summary>
    /// Handles the specified <see cref="CloudEventRecord"/> the way the live stream does
    /// </summary>
    public Task HandleCloudEventAsync(CloudEventRecord e) => this.OnCloudEventAsync(e);

    /// <summary>
    /// Catches up missed <see cref="CloudEvent"/>s
    /// </summary>
    public Task CatchUpMissedEventsAsync() => this.CatchUpAsync();

}
