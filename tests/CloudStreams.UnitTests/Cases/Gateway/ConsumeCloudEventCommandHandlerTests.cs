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

using CloudStreams.Gateway.Application.Services;
using CloudStreams.Gateway.Application.Commands.CloudEvents;
using Microsoft.Extensions.Logging.Abstractions;

namespace CloudStreams.UnitTests.Cases.Gateway;

/// <summary>
/// Covers the metrics recorded by the <see cref="ConsumeCloudEventCommandHandler"/> (see #202), and the rate limits and deduplication it applies (see #209 and #210)
/// </summary>
public class ConsumeCloudEventCommandHandlerTests
{

    static readonly CloudEvent Event = new() { Id = "1", SpecVersion = CloudEventSpecVersion.V1.Version, Source = new("https://orders.example.com"), Type = "order.placed" };

    static readonly CloudEventIngestionPolicy Policy = new()
    {
        Deduplication = new() { Window = Duration.FromMinutes(10) },
        RateLimits = [new() { Partition = CloudEventRateLimitPartitioning.Publisher, Limit = 10, Period = Duration.FromSeconds(1) }]
    };

    static ICloudEventAdmissionControl Admits(CloudEventIngestionPolicy? ingestion = null)
    {
        var admission = Substitute.For<ICloudEventAdmissionControl>();
        admission.EvaluateAsync(Arg.Any<CloudEvent>(), Arg.Any<IReadOnlyCollection<string>?>(), Arg.Any<CancellationToken>()).Returns(new OperationResult<CloudEventDescriptor>((int)HttpStatusCode.OK, new CloudEventDescriptor(new(), null)));
        admission.Gateway.Returns(new CloudStreams.Core.Resources.Gateway(new ResourceMetadata("gateway"), new GatewaySpec() { Ingestion = ingestion }));
        return admission;
    }

    static ICloudEventRateLimiter RateLimiter(RateLimitDecision decision)
    {
        var limiter = Substitute.For<ICloudEventRateLimiter>();
        limiter.EvaluateAsync(Arg.Any<string>(), Arg.Any<CloudEvent>(), Arg.Any<string?>(), Arg.Any<IReadOnlyList<CloudEventRateLimit>>(), Arg.Any<CancellationToken>()).Returns(decision);
        return limiter;
    }

    static ICloudEventDeduplicator Deduplicator(DeduplicationOutcome outcome)
    {
        var deduplicator = Substitute.For<ICloudEventDeduplicator>();
        deduplicator.TryReserveAsync(Arg.Any<string>(), Arg.Any<CloudEvent>(), Arg.Any<CancellationToken>()).Returns(outcome);
        return deduplicator;
    }

    static ConsumeCloudEventCommandHandler CreateHandler(ICloudEventAdmissionControl admission, IGatewayMetrics? metrics = null, ICloudEventStore? store = null, ICloudEventRateLimiter? rateLimiter = null, ICloudEventDeduplicator? deduplicator = null) =>
        new(NullLogger<ConsumeCloudEventCommandHandler>.Instance, admission, metrics ?? Substitute.For<IGatewayMetrics>(), store ?? Substitute.For<ICloudEventStore>(), rateLimiter ?? RateLimiter(RateLimitDecision.Allow), deduplicator ?? Deduplicator(DeduplicationOutcome.Reserved));

    [Fact]
    public async Task A_Stored_Event_Should_Be_Counted_As_Ingested_And_Its_Ingestion_Timed()
    {
        // arrange
        var metrics = Substitute.For<IGatewayMetrics>();
        var handler = CreateHandler(Admits(), metrics);

        // act
        var result = await handler.HandleAsync(new ConsumeEventCommand(Event), CancellationToken.None);

        // assert
        Assert.Equal((int)HttpStatusCode.Created, result.Status);
        metrics.Received(1).IncrementTotalIngestedEvents(Event.Source);
        metrics.Received(1).RecordIngestionDuration(IngestionOutcome.Stored, Arg.Any<TimeSpan>());
    }

    [Fact]
    public async Task An_Event_That_Fails_To_Be_Appended_Should_Be_Counted_As_An_Append_Failure()
    {
        // arrange
        var metrics = Substitute.For<IGatewayMetrics>();
        var store = Substitute.For<ICloudEventStore>();
        store.AppendAsync(Arg.Any<CloudEventDescriptor>(), Arg.Any<CancellationToken>()).Returns<Task<CloudEventRecord>>(_ => throw new InvalidOperationException("The event store is unavailable"));
        var handler = CreateHandler(Admits(), metrics, store);

        // act
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(new ConsumeEventCommand(Event), CancellationToken.None));

        // assert
        metrics.Received(1).IncrementTotalAppendFailures(Event.Source);
        metrics.DidNotReceive().IncrementTotalIngestedEvents(Arg.Any<Uri>());
        metrics.Received(1).RecordIngestionDuration(IngestionOutcome.Failed, Arg.Any<TimeSpan>());
    }

    [Theory]
    [InlineData(HttpStatusCode.Accepted, IngestionOutcome.Ignored)]
    [InlineData(HttpStatusCode.BadRequest, IngestionOutcome.Invalid)]
    [InlineData(HttpStatusCode.Forbidden, IngestionOutcome.Rejected)]
    [InlineData(HttpStatusCode.Conflict, IngestionOutcome.Rejected)]
    [InlineData(HttpStatusCode.ServiceUnavailable, IngestionOutcome.Failed)]
    public async Task An_Event_That_Is_Not_Admitted_Should_Have_Its_Ingestion_Timed_With_Its_Outcome(HttpStatusCode status, string outcome)
    {
        // arrange
        var metrics = Substitute.For<IGatewayMetrics>();
        var admission = Substitute.For<ICloudEventAdmissionControl>();
        admission.EvaluateAsync(Arg.Any<CloudEvent>(), Arg.Any<IReadOnlyCollection<string>?>(), Arg.Any<CancellationToken>()).Returns(new OperationResult<CloudEventDescriptor>((int)status));
        var store = Substitute.For<ICloudEventStore>();
        var handler = CreateHandler(admission, metrics, store);

        // act
        await handler.HandleAsync(new ConsumeEventCommand(Event), CancellationToken.None);

        // assert
        metrics.Received(1).RecordIngestionDuration(outcome, Arg.Any<TimeSpan>());
        await store.DidNotReceive().AppendAsync(Arg.Any<CloudEventDescriptor>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task An_Event_That_Exceeds_A_Rate_Limit_Should_Be_Refused_With_The_Delay_After_Which_To_Retry()
    {
        // arrange
        var metrics = Substitute.For<IGatewayMetrics>();
        var store = Substitute.For<ICloudEventStore>();
        var admission = Admits(Policy);
        var limiter = RateLimiter(new(false, TimeSpan.FromMilliseconds(1500)));
        var handler = CreateHandler(admission, metrics, store, limiter);

        // act
        var result = await handler.HandleAsync(new ConsumeEventCommand(Event, publisher: "orders"), CancellationToken.None);

        // assert
        Assert.Equal((int)HttpStatusCode.TooManyRequests, result.Status);
        Assert.Equal(TimeSpan.FromMilliseconds(1500), Assert.IsType<IngestionResult>(result).RetryAfter);
        await limiter.Received(1).EvaluateAsync("gateway", Event, "orders", Policy.RateLimits!, Arg.Any<CancellationToken>());
        await admission.DidNotReceive().EvaluateAsync(Arg.Any<CloudEvent>(), Arg.Any<IReadOnlyCollection<string>?>(), Arg.Any<CancellationToken>());
        await store.DidNotReceive().AppendAsync(Arg.Any<CloudEventDescriptor>(), Arg.Any<CancellationToken>());
        metrics.Received(1).IncrementTotalRejectedEvents(RejectionReason.RateLimit);
        metrics.Received(1).RecordIngestionDuration(IngestionOutcome.Rejected, Arg.Any<TimeSpan>());
    }

    [Fact]
    public async Task Without_An_Ingestion_Policy_Neither_Rate_Limits_Nor_Deduplication_Should_Apply()
    {
        // arrange
        var limiter = RateLimiter(new(false, TimeSpan.FromSeconds(1)));
        var deduplicator = Deduplicator(DeduplicationOutcome.Duplicate);
        var handler = CreateHandler(Admits(), rateLimiter: limiter, deduplicator: deduplicator);

        // act
        var result = await handler.HandleAsync(new ConsumeEventCommand(Event), CancellationToken.None);

        // assert
        Assert.Equal((int)HttpStatusCode.Created, result.Status);
        await limiter.DidNotReceiveWithAnyArgs().EvaluateAsync(default!, default!, default, default!, default);
        await deduplicator.DidNotReceiveWithAnyArgs().TryReserveAsync(default!, default!, default);
    }

    [Fact]
    public async Task A_Stored_Event_Should_Be_Remembered_For_The_Deduplication_Window()
    {
        // arrange
        var deduplicator = Deduplicator(DeduplicationOutcome.Reserved);
        var handler = CreateHandler(Admits(Policy), deduplicator: deduplicator);

        // act
        var result = await handler.HandleAsync(new ConsumeEventCommand(Event), CancellationToken.None);

        // assert
        Assert.Equal((int)HttpStatusCode.Created, result.Status);
        await deduplicator.Received(1).ConfirmAsync("gateway", Event, TimeSpan.FromMinutes(10), Arg.Any<CancellationToken>());
        await deduplicator.DidNotReceiveWithAnyArgs().ReleaseAsync(default!, default!, default);
    }

    [Fact]
    public async Task A_Duplicate_Should_Be_Answered_As_A_Success_Without_Being_Stored_Again()
    {
        // arrange
        var metrics = Substitute.For<IGatewayMetrics>();
        var store = Substitute.For<ICloudEventStore>();
        var handler = CreateHandler(Admits(Policy), metrics, store, deduplicator: Deduplicator(DeduplicationOutcome.Duplicate));

        // act
        var result = await handler.HandleAsync(new ConsumeEventCommand(Event), CancellationToken.None);

        // assert
        Assert.Equal((int)HttpStatusCode.OK, result.Status);
        await store.DidNotReceive().AppendAsync(Arg.Any<CloudEventDescriptor>(), Arg.Any<CancellationToken>());
        metrics.Received(1).IncrementTotalDuplicateEvents(Event.Source);
        metrics.Received(1).RecordIngestionDuration(IngestionOutcome.Duplicate, Arg.Any<TimeSpan>());
    }

    [Fact]
    public async Task An_Event_That_Is_Being_Ingested_Should_Be_Refused_As_Unavailable()
    {
        // arrange
        var store = Substitute.For<ICloudEventStore>();
        var handler = CreateHandler(Admits(Policy), store: store, deduplicator: Deduplicator(DeduplicationOutcome.InFlight));

        // act
        var result = await handler.HandleAsync(new ConsumeEventCommand(Event), CancellationToken.None);

        // assert - the first attempt may yet fail: the publisher must retry to find out
        Assert.Equal((int)HttpStatusCode.ServiceUnavailable, result.Status);
        Assert.NotNull(Assert.IsType<IngestionResult>(result).RetryAfter);
        await store.DidNotReceive().AppendAsync(Arg.Any<CloudEventDescriptor>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task An_Event_That_Fails_To_Be_Stored_Should_Be_Released_So_That_It_Is_Not_Mistaken_For_A_Duplicate()
    {
        // arrange
        var store = Substitute.For<ICloudEventStore>();
        store.AppendAsync(Arg.Any<CloudEventDescriptor>(), Arg.Any<CancellationToken>()).Returns<Task<CloudEventRecord>>(_ => throw new InvalidOperationException("The event store is unavailable"));
        var deduplicator = Deduplicator(DeduplicationOutcome.Reserved);
        var handler = CreateHandler(Admits(Policy), store: store, deduplicator: deduplicator);

        // act
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(new ConsumeEventCommand(Event), CancellationToken.None));

        // assert
        await deduplicator.Received(1).ReleaseAsync("gateway", Event, Arg.Any<CancellationToken>());
        await deduplicator.DidNotReceiveWithAnyArgs().ConfirmAsync(default!, default!, default, default);
    }

    [Fact]
    public async Task An_Event_That_Is_Not_Admitted_Should_Not_Be_Reserved()
    {
        // arrange - a refused event must not be remembered, or its corrected version, published with the same id, would be ignored
        var admission = Admits(Policy);
        admission.EvaluateAsync(Arg.Any<CloudEvent>(), Arg.Any<IReadOnlyCollection<string>?>(), Arg.Any<CancellationToken>()).Returns(new OperationResult<CloudEventDescriptor>((int)HttpStatusCode.BadRequest));
        var deduplicator = Deduplicator(DeduplicationOutcome.Reserved);
        var handler = CreateHandler(admission, deduplicator: deduplicator);

        // act
        await handler.HandleAsync(new ConsumeEventCommand(Event), CancellationToken.None);

        // assert
        await deduplicator.DidNotReceiveWithAnyArgs().TryReserveAsync(default!, default!, default);
    }

}
