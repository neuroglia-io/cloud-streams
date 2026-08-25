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
/// Covers the combination production always takes and the rest of this suite never did: a live event dispatched with both
/// <c>retryOnError</c> and <c>catchUpWhenAvailable</c> set, against the real catch-up loop.
/// </summary>
/// <remarks>
/// Once the retry policy owns the first delivery attempt, <c>RetryDispatchAsync</c> is the normal path rather than the failure
/// path, and everything trailing its <c>ExecuteAsync</c> runs after every healthy dispatch too. Every other broker case drives
/// dispatch with <c>catchUpWhenAvailable: false</c> or suppresses catch-up outright, so that shift is invisible to them.
/// </remarks>
public class SubscriptionHandlerLivePathTests
{

    [Fact]
    public async Task Live_Dispatch_Should_Not_Catch_Up_After_A_Successful_Delivery()
    {
        //arrange
        //a subscription that tracks no offset resolves its catch-up start position to the *last* record in the store, which is
        //the event that was just delivered - so an unconditional catch-up here hands the subscriber every event twice
        await using var context = await SubscriptionHandlerTestContext.CreateAsync(initializeStream: true, offset: null, seed: [SubscriptionFactory.CloudEventRecord(0)]);
        context.Http.Respond(HttpStatusCode.OK);
        context.Handler.SuppressAutomaticCatchUp = false;

        //act
        await context.Handler.DispatchPublicAsync(SubscriptionFactory.CloudEvent(), 0, retryOnError: true, catchUpWhenAvailable: true);

        //assert
        context.Http.RequestCount.Should().Be(1, "a delivery that never failed has nothing to catch up on");
        context.Handler.IsOutOfSync.Should().BeFalse();
    }

    [Fact]
    public async Task Live_Dispatch_Should_Not_Poll_The_Store_After_A_Successful_Delivery()
    {
        //arrange
        //the offset-tracking shape does not redeliver, it stalls: the catch-up reads the next position, finds nothing, and
        //waits 50ms before its loop condition lets it exit - once per event, with the live path latched out of sync throughout
        await using var context = await SubscriptionHandlerTestContext.CreateAsync(initializeStream: true, seed: [SubscriptionFactory.CloudEventRecord(0)]);
        context.Http.Respond(HttpStatusCode.OK);
        context.Handler.SuppressAutomaticCatchUp = false;
        var readsBefore = context.EventStore.ReadCount;

        //act
        await context.Handler.DispatchPublicAsync(SubscriptionFactory.CloudEvent(), 0, retryOnError: true, catchUpWhenAvailable: true);

        //assert
        context.EventStore.ReadCount.Should().Be(readsBefore, "a healthy dispatch must not read the store at all");
    }

    [Fact]
    public async Task Live_Dispatch_Should_Catch_Up_After_A_Recovery()
    {
        //arrange
        //the other half of the contract: the catch-up is gated on the delivery having failed, not removed. A subscriber that
        //went away and came back missed live events while it was down, and they have to be replayed
        using var clock = RecordingSystemClock.Install();
        await using var context = await SubscriptionHandlerTestContext.CreateAsync(SubscriptionFactory.RetryPolicy(maxAttempts: 3), initializeStream: true, seed: [SubscriptionFactory.CloudEventRecord(0), SubscriptionFactory.CloudEventRecord(1)]);
        context.Http.Script(HttpStatusCode.ServiceUnavailable, HttpStatusCode.OK);
        context.Handler.SuppressAutomaticCatchUp = false;

        //act
        await context.Handler.DispatchPublicAsync(SubscriptionFactory.CloudEvent(), 0, retryOnError: true, catchUpWhenAvailable: true);

        //assert
        context.Http.RequestCount.Should().Be(3, "one failed attempt, the retry that succeeded, and the record the catch-up replayed");
        context.Handler.IsOutOfSync.Should().BeFalse();
    }

    [Fact]
    public async Task Live_Event_Should_Be_Delivered_Exactly_Once()
    {
        //arrange
        //the whole chain, driven the way the broker drives it: a record pushed through the observed stream, into
        //OnCloudEventAsync, and out to the subscriber
        await using var context = await SubscriptionHandlerTestContext.CreateAsync(initializeStream: true, offset: null);
        context.Http.Respond(HttpStatusCode.OK);
        context.Handler.SuppressAutomaticCatchUp = false;
        var record = SubscriptionFactory.CloudEventRecord(0);
        context.EventStore.With(record);

        //act
        context.EventStore.Stream.OnNext(record);
        await context.WaitForRequestsAsync(1);
        //the duplicate this guards against is issued from within the same dispatch, so settling once is enough to see it
        await Task.Delay(250);

        //assert
        context.Http.RequestCount.Should().Be(1, "a live event is delivered once, not once per catch-up the dispatch starts on its own");
    }

}
