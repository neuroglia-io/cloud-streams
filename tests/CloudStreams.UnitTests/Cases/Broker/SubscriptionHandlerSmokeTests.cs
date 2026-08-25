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
/// Exercises the happy path end to end, so that the fixture the rest of the broker cases are built on is itself covered
/// </summary>
public class SubscriptionHandlerSmokeTests
{

    [Fact]
    public async Task Dispatch_Should_Deliver_The_Event_To_The_Subscriber()
    {
        //arrange
        await using var context = await SubscriptionHandlerTestContext.CreateAsync();
        context.Http.Respond(HttpStatusCode.OK);

        //act
        await context.Handler.DispatchPublicAsync(SubscriptionFactory.CloudEvent(), 0);

        //assert
        context.Http.RequestCount.Should().Be(1);
        context.Http.Requests[0].RequestUri.Should().Be(SubscriptionFactory.SubscriberUri);
        context.Http.Requests[0].Method.Should().Be(HttpMethod.Post);
    }

    [Fact]
    public async Task Dispatch_Should_Increment_TotalPublishedEvents_On_Success()
    {
        //arrange
        await using var context = await SubscriptionHandlerTestContext.CreateAsync();
        context.Http.Respond(HttpStatusCode.OK);

        //act
        await context.Handler.DispatchPublicAsync(SubscriptionFactory.CloudEvent(), 0);

        //assert
        context.Metrics.TotalPublishedEvents.Should().Be(1);
        context.Metrics.TotalDeliveryFailures.Should().Be(0);
    }

    [Fact]
    public async Task Dispatch_Should_Commit_The_Next_Offset_On_Success()
    {
        //arrange
        await using var context = await SubscriptionHandlerTestContext.CreateAsync();
        context.Http.Respond(HttpStatusCode.OK);

        //act
        await context.Handler.DispatchPublicAsync(SubscriptionFactory.CloudEvent(), 41);

        //assert
        context.Repository.LastValueAt("/status/stream/ackedOffset").Should().Be("42");
    }

    [Fact]
    public async Task Initialize_Should_Report_The_Subscription_As_Active()
    {
        //arrange & act
        await using var context = await SubscriptionHandlerTestContext.CreateAsync();

        //assert
        context.Repository.LastValueAt("/status/phase").Should().Be("active");
    }

    [Fact]
    public async Task SetSubscriberState_Should_Patch_The_Subscription_Status()
    {
        //arrange
        await using var context = await SubscriptionHandlerTestContext.CreateAsync();

        //act
        await context.Handler.SetSubscriberStatePublicAsync(SubscriberState.Unreachable, "because");

        //assert
        context.Repository.LastValueAt("/status/subscriber/state").Should().Be("unreachable");
        context.Repository.LastValueAt("/status/subscriber/reason").Should().Be("because");
    }

}
