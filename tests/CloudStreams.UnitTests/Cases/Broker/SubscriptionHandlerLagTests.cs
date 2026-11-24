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
/// Covers the lag the subscription handler reports to the broker's lag gauge (see #202)
/// </summary>
public class SubscriptionHandlerLagTests
{

    [Fact]
    public async Task A_Subscription_Up_To_Date_Should_Report_No_Lag()
    {
        // arrange
        await using var context = await BrokerTestContext.CreateAsync(streamLength: 3);

        // assert
        Assert.True(context.Metrics.ObservesLag);
        Assert.Equal(("All", 0ul), context.Metrics.Lag);
    }

    [Fact]
    public async Task A_Subscription_Whose_Delivery_Fails_Should_Report_The_Records_It_Has_Yet_To_Deliver()
    {
        // arrange - the subscriber is down, and three records have been published since the subscription last acknowledged one
        await using var context = await BrokerTestContext.CreateAsync(StubHttpMessageHandler.AlwaysResponds(HttpStatusCode.ServiceUnavailable), streamLength: 5,
            configureSubscription: s =>
            {
                s.Spec.Subscriber.RetryPolicy = new() { MaxAttempts = 0 };
                s.Spec.Stream!.Offset = 2;
                s.Status!.Stream!.AckedOffset = 2;
            }, waitForStreamInitialization: false);

        // assert
        await WaitUntilAsync(() => context.Http.AttemptCount >= 1 && context.Metrics.Lag is { Lag: 3 }, "the lag to be reported");
        Assert.Equal("All", context.Metrics.Lag!.Value.Stream);
    }

    [Fact]
    public async Task A_Pending_Acknowledgement_Should_Count_The_Event_Awaiting_It()
    {
        // arrange
        await using var context = await BrokerTestContext.CreateAsync(StubHttpMessageHandler.AlwaysResponds(HttpStatusCode.Accepted), streamLength: 2);

        // act
        await context.Handler.DispatchRecordAsync(context.EventStore.Records[1]);

        // assert
        Assert.Equal(1ul, context.Metrics.Lag!.Value.Lag);
    }

    [Fact]
    public async Task A_Disposed_Handler_Should_Stop_Reporting_Its_Lag()
    {
        // arrange
        var context = await BrokerTestContext.CreateAsync();

        // act
        await context.DisposeAsync();

        // assert
        Assert.False(context.Metrics.ObservesLag);
    }

    static async Task WaitUntilAsync(Func<bool> condition, string description)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException($"Timed out waiting for {description}");
            await Task.Delay(20);
        }
    }

}
