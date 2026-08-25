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
/// Covers the reporting of subscriber reachability. <c>SubscriberState.Reachable</c> used to be written only after a recovery,
/// so a subscriber that had never failed reported <c>unknown</c> forever. See
/// <see href="https://github.com/neuroglia-io/cloud-streams/issues/104">#104</see>.
/// </summary>
public class SubscriptionHandlerSubscriberStateTests
{

    [Fact]
    public async Task Dispatch_Should_Report_The_Subscriber_As_Reachable_On_First_Success()
    {
        //arrange
        await using var context = await SubscriptionHandlerTestContext.CreateAsync();
        context.Http.Respond(HttpStatusCode.OK);

        //act
        await context.Handler.DispatchPublicAsync(SubscriptionFactory.CloudEvent(), 0);

        //assert
        context.Repository.LastValueAt("/status/subscriber/state").Should().Be("reachable", "a subscriber that has never failed is still reachable");
    }

    [Fact]
    public async Task Dispatch_Should_Report_The_Subscriber_As_Reachable_Only_Once()
    {
        //arrange
        //SetSubscriberStateAsync early-returns on an unchanged state, but it compares against this.Subscription, which is
        //refreshed by the resource watch and therefore lags. The in-memory last-reported pair closes that window, which is
        //what stops a healthy subscription patching the resource store once per event
        await using var context = await SubscriptionHandlerTestContext.CreateAsync();
        context.Http.Respond(HttpStatusCode.OK);

        //act
        for (var i = 0; i < 5; i++) await context.Handler.DispatchPublicAsync(SubscriptionFactory.CloudEvent(), (ulong)i);

        //assert
        context.Repository.WriteCountAt("/status/subscriber/state").Should().Be(1, "a healthy subscription writes its subscriber state once, not once per event");
        context.Metrics.TotalPublishedEvents.Should().Be(5);
    }

    [Fact]
    public async Task Dispatch_Should_Report_The_Subscriber_As_Reachable_After_A_Recovery()
    {
        //arrange
        using var clock = RecordingSystemClock.Install();
        await using var context = await SubscriptionHandlerTestContext.CreateAsync(SubscriptionFactory.RetryPolicy(maxAttempts: 3));
        context.Http.Script(HttpStatusCode.ServiceUnavailable, HttpStatusCode.OK);

        //act
        await context.Handler.DispatchPublicAsync(SubscriptionFactory.CloudEvent(), 0);

        //assert
        context.Repository.ValuesAt("/status/subscriber/state").Should().ContainInOrder("unreachable", "reachable");
    }

}
