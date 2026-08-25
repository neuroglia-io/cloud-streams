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

namespace CloudStreams.UnitTests.Cases.Broker;

/// <summary>
/// Covers the <c>SubscriptionOutOfSync</c> latch, which <c>CatchUpAsync</c> used to clear on its loop's success line rather
/// than in a <c>finally</c>. Any exit that was not the loop condition going false therefore left the subscription permanently
/// deaf to live events, with nothing logged. This is the shared mechanism behind
/// <see href="https://github.com/neuroglia-io/cloud-streams/issues/132">#132</see>,
/// <see href="https://github.com/neuroglia-io/cloud-streams/issues/105">#105</see>,
/// <see href="https://github.com/neuroglia-io/cloud-streams/issues/81">#81</see> and
/// <see href="https://github.com/neuroglia-io/cloud-streams/issues/28">#28</see>.
/// </summary>
/// <remarks>
/// Every test here drives catch-up explicitly. Stream initialization starts a catch-up of its own whenever
/// <c>Status.ObservedGeneration</c> is null, and against a fixture whose resource is never written back that is always — so
/// the fixture suppresses it, otherwise two loops would be racing over the same fakes.
/// </remarks>
public class SubscriptionHandlerCatchUpTests
{

    [Fact]
    public async Task CatchUp_Should_Clear_The_OutOfSync_Latch_When_The_Loop_Throws()
    {
        //arrange
        await using var context = await SubscriptionHandlerTestContext.CreateAsync(initializeStream: true, seed: [SubscriptionFactory.CloudEventRecord(0), SubscriptionFactory.CloudEventRecord(1)]);
        context.Http.Respond(HttpStatusCode.OK);
        //fault the read that follows the first dispatched record, so the loop fails part-way through the backlog rather
        //than before it has done anything
        context.EventStore.ReadFault = new InvalidOperationException("the event store fell over mid-iteration");
        context.EventStore.ReadFaultAfter = context.EventStore.ReadCount + 1;

        //act
        await context.Handler.CatchUpPublicAsync();

        //assert
        context.Http.RequestCount.Should().Be(1, "the loop should have dispatched one record before failing");
        context.Handler.IsOutOfSync.Should().BeFalse("an early exit must not leave the subscription dropping live events");
    }

    [Fact]
    public async Task CatchUp_Should_Record_A_Fault_When_The_Loop_Throws()
    {
        //arrange
        //today a non-cancellation exception escapes a fire-and-forget task unobserved: no log, no fault, no fix.
        //This is #28's original ask, which was to log the error and continue rather than fail silently
        await using var context = await SubscriptionHandlerTestContext.CreateAsync(initializeStream: true, seed: [SubscriptionFactory.CloudEventRecord(0)]);
        context.Http.Respond(HttpStatusCode.OK);
        context.EventStore.ReadFault = new InvalidOperationException("the event store fell over mid-iteration");

        //act
        await context.Handler.CatchUpPublicAsync();

        //assert
        context.Repository.LastValueAt("/status/stream/fault").Should().NotBeNull();
    }

    [Fact]
    public async Task CatchUp_Should_Clear_The_OutOfSync_Latch_When_Cancelled()
    {
        //arrange
        //the cancellation filter swallows the exception, so the loop exits without ever reaching the line that clears the latch
        await using var context = await SubscriptionHandlerTestContext.CreateAsync(initializeStream: true, seed: [SubscriptionFactory.CloudEventRecord(0)]);
        context.Http.Respond(HttpStatusCode.OK);
        context.EventStore.ReadFault = new OperationCanceledException();

        //act
        await context.Handler.CatchUpPublicAsync();

        //assert
        context.Handler.IsOutOfSync.Should().BeFalse();
    }

    [Fact]
    public async Task CatchUp_Should_Deliver_The_Backlog_And_Then_Release_The_Subscription()
    {
        //arrange
        //the loop bounds itself with 'currentOffset <= StreamOffset'. While StreamOffset held a *length* it polled a position
        //that cannot exist, every 50ms, forever: the success line was unreachable in the steady state, so the latch stayed set
        //even when catch-up succeeded and the live path stayed short-circuited
        await using var context = await SubscriptionHandlerTestContext.CreateAsync(initializeStream: true, seed: [SubscriptionFactory.CloudEventRecord(0), SubscriptionFactory.CloudEventRecord(1)]);
        context.Http.Respond(HttpStatusCode.OK);

        //act
        var catchUp = context.Handler.CatchUpPublicAsync();
        await context.WaitForRequestsAsync(2);
        await catchUp.WaitAsync(TimeSpan.FromSeconds(5));

        //assert
        context.Http.RequestCount.Should().Be(2, "the backlog is two records and there is nothing after it");
        context.Handler.IsOutOfSync.Should().BeFalse("a subscription that has caught up must go back to receiving live events");
    }

    [Fact]
    public async Task Initialization_Should_Record_The_Offset_Of_The_Last_Record_Not_The_Stream_Length()
    {
        //arrange
        //the two writers of StreamOffset must agree: initialization reads a length, OnCloudEventAsync writes a sequence.
        //Storing the length here is the off-by-one that makes the catch-up loop unable to terminate
        await using var context = await SubscriptionHandlerTestContext.CreateAsync(initializeStream: true, seed: [SubscriptionFactory.CloudEventRecord(0), SubscriptionFactory.CloudEventRecord(1)]);

        //act
        //initialization is awaited by the fixture

        //assert
        context.Handler.LastKnownStreamOffset.Should().Be(1, "two records occupy offsets 0 and 1");
    }

    [Fact]
    public async Task Dispatch_Should_Redeliver_After_A_Subscriber_Timeout()
    {
        //arrange
        //the discriminator between a timeout and a shutdown is the handler's own token: on shutdown it is cancelled, on a
        //timeout it is not. Without that distinction the event is skipped in silence, with no fault and no metric
        using var clock = RecordingSystemClock.Install();
        await using var context = await SubscriptionHandlerTestContext.CreateAsync(SubscriptionFactory.RetryPolicy(maxAttempts: 2), httpTimeout: TimeSpan.FromMilliseconds(50));
        context.Http.Delay(TimeSpan.FromSeconds(30));

        //act
        await context.Handler.DispatchPublicAsync(SubscriptionFactory.CloudEvent(), 0);

        //assert
        context.Http.RequestCount.Should().Be(2, "a timeout is a delivery failure and must be retried, not skipped");
        context.Metrics.TotalDeliveryFailures.Should().BeGreaterThan(0);
    }

}
