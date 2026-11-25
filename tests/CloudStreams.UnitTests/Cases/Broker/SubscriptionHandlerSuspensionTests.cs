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
/// Covers the suspension and resumption of subscriptions (see #214)
/// </summary>
public class SubscriptionHandlerSuspensionTests
{

    static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    static async Task WaitUntilAsync(Func<bool> condition, string description)
    {
        var deadline = DateTimeOffset.UtcNow + Timeout;
        while (!condition())
        {
            if (DateTimeOffset.UtcNow >= deadline) throw new TimeoutException($"Timed out waiting until {description}");
            await Task.Delay(10);
        }
    }

    static bool ReportedPhase(BrokerTestContext context, string phase) => context.Patches.ValuesWrittenTo("/status/phase").Any(v => v.GetValue<string>() == phase);

    /// <summary>
    /// Creates a subscription that has been suspended while its broker was running: the broker has observed the generation that suspended it, and acknowledged the
    /// events at offsets 0 to 2
    /// </summary>
    static void Suspended(Subscription subscription)
    {
        subscription.Metadata.Generation = 2;
        subscription.Spec.Suspended = true;
        subscription.Status!.ObservedGeneration = 2;
        // the broker reports its subscriptions as inactive when it stops
        subscription.Status.Phase = SubscriptionStatusPhase.Inactive;
        subscription.Status.Stream!.AckedOffset = 3;
    }

    [Fact]
    public async Task Suspending_Should_Stop_Delivering_And_Keep_The_Acknowledged_Offset_Valid()
    {
        // arrange
        await using var context = await BrokerTestContext.CreateAsync();
        var suspended = context.Subscription.Clone()!;
        suspended.Metadata.Generation = 2;
        suspended.Spec.Suspended = true;

        // act
        context.PublishSubscriptionUpdate(suspended);
        await WaitUntilAsync(() => ReportedPhase(context, "suspended"), "the subscription is reported as suspended");
        context.EventStore.Publish();
        await context.Handler.HandleCloudEventAsync(BrokerTestContext.CreateRecord(0));
        await Task.Delay(200);

        // assert
        Assert.Equal(0, context.Http.AttemptCount);
        // suspending changed the generation, which would otherwise invalidate the acknowledged offset
        Assert.Contains(context.Patches.ValuesWrittenTo("/status/observedGeneration"), v => v.GetValue<ulong>() == 2);
    }

    [Fact]
    public async Task A_Subscription_Suspended_At_Startup_Should_Deliver_Nothing()
    {
        // arrange
        await using var context = await BrokerTestContext.CreateAsync(streamLength: 5, waitForStreamInitialization: false, configureSubscription: Suspended);

        // act
        await Task.Delay(300);

        // assert
        Assert.Equal(0, context.Http.AttemptCount);
        Assert.True(ReportedPhase(context, "suspended"));
        Assert.False(ReportedPhase(context, "active"));
    }

    [Fact]
    public async Task Resuming_Should_Deliver_What_Was_Published_While_Suspended_From_The_Acknowledged_Offset()
    {
        // arrange - the subscription tracks the end of the stream: without its acknowledged offset, it would skip what was published while it was suspended
        await using var context = await BrokerTestContext.CreateAsync(streamLength: 5, waitForStreamInitialization: false, configureSubscription: Suspended);
        await WaitUntilAsync(() => ReportedPhase(context, "suspended"), "the subscription is reported as suspended");
        var resumed = context.Subscription.Clone()!;
        resumed.Metadata.Generation = 3;
        resumed.Spec.Suspended = false;

        // act
        context.PublishSubscriptionUpdate(resumed);
        await WaitUntilAsync(() => context.Http.AttemptCount >= 2, "the events published while suspended are delivered");
        await Task.Delay(200);

        // assert
        Assert.Equal(["3", "4"], context.Http.Attempts.Select(a => a.Headers[CloudStreamsHeaders.Offset]));
        Assert.True(ReportedPhase(context, "active"));
        Assert.Contains(context.Patches.ValuesWrittenTo("/status/observedGeneration"), v => v.GetValue<ulong>() == 3);
    }

    [Fact]
    public async Task A_Subscription_That_Has_Delivered_Nothing_Should_Resume_Where_It_Was_Suspended()
    {
        // arrange - the subscription tracks the end of a stream of 2 events, and has committed no offset: the end of the stream is its only position
        await using var context = await BrokerTestContext.CreateAsync(streamLength: 2);
        var suspended = context.Subscription.Clone()!;
        suspended.Metadata.Generation = 2;
        suspended.Spec.Suspended = true;

        // act - the position reached is committed on suspension
        context.PublishSubscriptionUpdate(suspended);
        await WaitUntilAsync(() => ReportedPhase(context, "suspended"), "the subscription is reported as suspended");
        Assert.Equal([2ul], context.Patches.OffsetsWrittenTo());
        for (var i = 0; i < 3; i++) context.EventStore.Publish();

        // act - the subscription is resumed, once the watch has reported the commit
        var resumed = suspended.Clone()!;
        resumed.Metadata.Generation = 3;
        resumed.Spec.Suspended = false;
        resumed.Status!.ObservedGeneration = 2;
        resumed.Status.Stream!.AckedOffset = 2;
        context.PublishSubscriptionUpdate(resumed);
        await WaitUntilAsync(() => context.Http.AttemptCount >= 3, "the events published while suspended are delivered");
        await Task.Delay(200);

        // assert
        Assert.Equal(["2", "3", "4"], context.Http.Attempts.Select(a => a.Headers[CloudStreamsHeaders.Offset]));
    }

    [Fact]
    public async Task Changing_The_Offset_While_Suspended_Should_Reset_It_At_Once_And_Apply_It_On_Resumption()
    {
        // arrange
        await using var context = await BrokerTestContext.CreateAsync(streamLength: 5, waitForStreamInitialization: false, configureSubscription: Suspended);
        await WaitUntilAsync(() => ReportedPhase(context, "suspended"), "the subscription is reported as suspended");
        var rewound = context.Subscription.Clone()!;
        rewound.Metadata.Generation = 3;
        rewound.Spec.Stream = new() { Offset = StreamPosition.StartOfStream };

        // act - the offset is changed while suspended
        context.PublishSubscriptionUpdate(rewound);
        await WaitUntilAsync(() => context.Patches.Touched("/status/stream/ackedOffset"), "the acknowledged offset is reset");
        await Task.Delay(200);

        // assert - nothing is delivered while suspended
        Assert.Equal(0, context.Http.AttemptCount);

        // act - the subscription is resumed, once the watch has reported the reset
        var resumed = rewound.Clone()!;
        resumed.Metadata.Generation = 4;
        resumed.Spec.Suspended = false;
        resumed.Status!.ObservedGeneration = 3;
        resumed.Status.Stream!.AckedOffset = null;
        context.PublishSubscriptionUpdate(resumed);
        await WaitUntilAsync(() => context.Http.AttemptCount >= 5, "the stream is delivered from its start");
        await Task.Delay(200);

        // assert
        Assert.Equal(["0", "1", "2", "3", "4"], context.Http.Attempts.Select(a => a.Headers[CloudStreamsHeaders.Offset]));
    }

}
