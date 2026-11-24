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
/// Covers a subscription that starts behind the head of its stream and has to replay what it missed
/// </summary>
public class SubscriptionHandlerCatchUpTests
{

    /// <summary>
    /// Partial regression guard for issue #81: a subscription sitting behind the latest offset must replay the events it missed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This asserts <em>delivery only</em>. Completing catch-up is covered by <see cref="Catch_Up_Should_Complete_And_Resume_Live_Dispatch"/>.
    /// </para>
    /// <para>
    /// Initialization starts catch-up itself when the resolved offset is behind the stream length, so this asserts on the automatic
    /// path rather than calling it directly. Recorders are deliberately not reset (<c>waitForStreamInitialization: false</c>) because
    /// the dispatches being asserted on happen during initialization.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task A_Subscription_Behind_The_Head_Should_Replay_The_Events_It_Missed()
    {
        // arrange
        await using var context = await BrokerTestContext.CreateAsync(streamLength: 3,
            configureSubscription: s => s.Spec.Stream = new() { Offset = StreamPosition.StartOfStream },
            waitForStreamInitialization: false);

        // act
        await WaitUntilAsync(() => context.Http.AttemptCount >= 3, "the three missed events to be replayed", context);

        // assert
        Assert.Equal(3, context.Http.AttemptCount);
        Assert.Equal(3, context.Metrics.TotalPublishedEvents);
        // offsets are committed as "the next one to read", so replaying records 0, 1 and 2 commits 1, 2 and 3
        Assert.Equal([1ul, 2ul, 3ul], context.Patches.OffsetsWrittenTo());
    }

    /// <summary>
    /// Regression guard for the other half of issue #81: replaying the missed events must also finish catch-up, so live events flow again.
    /// </summary>
    /// <remarks>
    /// <c>CatchUpAsync</c> used to exit on <c>currentOffset &lt;= StreamOffset</c> while <c>StreamOffset</c> meant a length at initialization and
    /// an index on the live path, so it waited forever for a record past the end and never left the out-of-sync state, dropping every live event
    /// ("appears stuck", #81 and #105). <c>StreamOffset</c> now always means the length of the stream.
    /// </remarks>
    [Fact]
    public async Task Catch_Up_Should_Complete_And_Resume_Live_Dispatch()
    {
        // arrange
        await using var context = await BrokerTestContext.CreateAsync(streamLength: 3,
            configureSubscription: s => s.Spec.Stream = new() { Offset = StreamPosition.StartOfStream },
            waitForStreamInitialization: false);
        await WaitUntilAsync(() => context.Http.AttemptCount >= 3, "the three missed events to be replayed", context);
        await WaitUntilAsync(() => !context.Handler.IsOutOfSync, "catch-up to complete", context);

        // act
        context.ResetRecorders();
        await context.Handler.HandleCloudEventAsync(BrokerTestContext.CreateRecord(3));

        // assert
        Assert.Equal(1, context.Http.AttemptCount);
        Assert.Contains(4ul, context.Patches.OffsetsWrittenTo());
    }

    /// <summary>
    /// Regression guard for issue #132: a catch-up that is aborted must not leave the subscription out of sync, which would silently drop every live event.
    /// </summary>
    [Fact]
    public async Task Aborted_Catch_Up_Should_Not_Leave_The_Subscription_Out_Of_Sync()
    {
        // arrange - the subscriber never recovers, so catch-up gives up on the first missed event
        await using var context = await BrokerTestContext.CreateAsync(StubHttpMessageHandler.AlwaysResponds(HttpStatusCode.InternalServerError), streamLength: 3,
            configureSubscription: s =>
            {
                s.Spec.Stream = new() { Offset = StreamPosition.StartOfStream };
                s.Spec.Subscriber.RetryPolicy = new() { MaxAttempts = 1, BackoffDuration = RetryBackoffDuration.Constant(Duration.FromSeconds(0)) };
            },
            waitForStreamInitialization: false);

        // act
        await WaitUntilAsync(() => context.Patches.Touched("/status/stream/fault"), "the subscription to be faulted", context);
        await WaitUntilAsync(() => !context.Handler.IsOutOfSync, "the out-of-sync state to be reset", context);

        // assert - the event that could not be delivered is never skipped
        Assert.Equal(2, context.Http.AttemptCount);
        Assert.False(context.Patches.Touched("/status/stream/ackedOffset"));
    }

    /// <summary>
    /// A subscription already at the head has nothing to replay, and must not re-dispatch what it has already acknowledged.
    /// </summary>
    [Fact]
    public async Task A_Subscription_At_The_Head_Should_Not_Replay_Anything()
    {
        // arrange
        await using var context = await BrokerTestContext.CreateAsync(streamLength: 3,
            configureSubscription: s => s.Status!.Stream!.AckedOffset = 3);

        // act
        await Task.Delay(200);

        // assert
        Assert.Equal(0, context.Http.AttemptCount);
    }

    static async Task WaitUntilAsync(Func<bool> condition, string description, BrokerTestContext context, TimeSpan? timeout = null)
    {
        var effectiveTimeout = timeout ?? TimeSpan.FromSeconds(10);
        var deadline = DateTimeOffset.UtcNow + effectiveTimeout;
        while (!condition())
        {
            if (DateTimeOffset.UtcNow >= deadline) throw new TimeoutException($"Timed out after {effectiveTimeout} waiting for {description}. Attempts: {context.Http.AttemptCount}. Recorded operations:{Environment.NewLine}{context.Patches.Describe()}");
            await Task.Delay(10).ConfigureAwait(false);
        }
    }

}
