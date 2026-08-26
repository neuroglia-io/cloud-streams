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
/// Covers the way a <see cref="SubscriptionHandler"/> dispatches cloud events to its subscriber
/// </summary>
public class SubscriptionHandlerTests
{

    [Fact]
    public async Task Dispatch_Should_Post_The_Event_To_The_Subscriber_And_Commit_The_Next_Offset()
    {
        // arrange
        await using var context = await BrokerTestContext.CreateAsync(StubHttpMessageHandler.AlwaysResponds(HttpStatusCode.OK));

        // act
        await context.Handler.DispatchRecordAsync(BrokerTestContext.CreateRecord(41));

        // assert
        Assert.Equal(1, context.Http.AttemptCount);
        Assert.Equal(new Uri("http://localhost:8888/events"), context.Http.Attempts[0].RequestUri);
        Assert.Equal(1, context.Metrics.TotalPublishedEvents);
        Assert.Equal(0, context.Metrics.TotalDeliveryFailures);
        Assert.True(context.Patches.Touched("/status/stream/ackedOffset"), context.Patches.Describe());
        Assert.Contains(42ul, context.Patches.OffsetsWrittenTo());
    }

    [Fact]
    public async Task Dispatch_Should_Eventually_Deliver_When_The_Subscriber_Recovers()
    {
        // arrange
        await using var context = await BrokerTestContext.CreateAsync(StubHttpMessageHandler.FailsThenSucceeds(1),
            configureSubscription: s => s.Spec.Subscriber.RetryPolicy = new() { MaxAttempts = 3, BackoffDuration = RetryBackoffDuration.Constant(Duration.FromSeconds(0)) });

        // act
        await context.Handler.DispatchRecordAsync(BrokerTestContext.CreateRecord(41));

        // assert
        Assert.Equal(2, context.Http.AttemptCount);
        Assert.Equal(1, context.Metrics.TotalPublishedEvents);
        Assert.Equal(1, context.Metrics.TotalDeliveryFailures);
        Assert.Contains(42ul, context.Patches.OffsetsWrittenTo());
    }

    [Fact]
    public async Task Live_Events_Should_Be_Dropped_While_The_Subscription_Is_Out_Of_Sync()
    {
        // arrange
        await using var context = await BrokerTestContext.CreateAsync();
        context.Handler.IsOutOfSync = true;

        // act
        await context.Handler.HandleCloudEventAsync(BrokerTestContext.CreateRecord(41));

        // assert
        Assert.Equal(0, context.Http.AttemptCount);
        Assert.Equal(0, context.Patches.Count);
    }

    [Fact]
    public async Task Live_Events_Should_Be_Dispatched_As_They_Are_Published()
    {
        // arrange
        await using var context = await BrokerTestContext.CreateAsync(streamLength: 2);

        // act
        context.EventStore.Publish();

        // assert
        await WaitUntilAsync(() => context.Http.AttemptCount == 1, "the published event to be dispatched");
        Assert.Equal(1, context.Metrics.TotalPublishedEvents);
        Assert.Contains(3ul, context.Patches.OffsetsWrittenTo());
    }

    /// <summary>
    /// Polls until the specified condition holds, so a test can assert on the asynchronous stream pump without sleeping for a fixed duration
    /// </summary>
    static async Task WaitUntilAsync(Func<bool> condition, string description, TimeSpan? timeout = null)
    {
        var effectiveTimeout = timeout ?? TimeSpan.FromSeconds(5);
        var deadline = DateTimeOffset.UtcNow + effectiveTimeout;
        while (!condition())
        {
            if (DateTimeOffset.UtcNow >= deadline) throw new TimeoutException($"Timed out after {effectiveTimeout} waiting for {description}.");
            await Task.Delay(10).ConfigureAwait(false);
        }
    }

}
