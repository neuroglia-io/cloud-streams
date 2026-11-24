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
    /// This asserts <em>delivery only</em>. It deliberately does not claim catch-up finished, because it does not:
    /// see <see cref="Catch_Up_Currently_Never_Completes_And_Leaves_The_Subscription_Out_Of_Sync"/>.
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
    /// Pins the other half of issue #81, which is still broken: replaying the missed events is not the same as finishing catch-up.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is a characterisation test. It asserts broken behaviour on purpose so the gap is visible in the suite, and so it fails
    /// loudly when the fix lands. <b>PR 3 (#132) must invert it and rename it.</b>
    /// </para>
    /// <para>
    /// The mechanism: <c>CatchUpAsync</c> exits on <c>(ulong)currentOffset &lt;= this.StreamOffset</c>. With three records,
    /// <c>StreamOffset</c> is the length (3) and <c>currentOffset</c> reaches 3 after the last dispatch, so <c>3 &lt;= 3</c> still
    /// holds. The loop then reads a fourth record that does not exist, gets null, waits 50 ms and repeats forever, never reaching
    /// <c>SubscriptionOutOfSync = false</c>. The guard in <c>OnCloudEventAsync</c> consequently drops every live event, which is
    /// exactly the "appears stuck, subscriber never receives events" symptom of #81 and #105.
    /// </para>
    /// <para>
    /// Fixing the boundary is not a one-line change, which is why it is deferred rather than patched here: <c>StreamOffset</c> carries
    /// two different meanings. Initialization sets it from <c>GetStreamMetadataAsync().Length</c> (a count), while
    /// <c>OnCloudEventAsync</c> sets it from <c>e.Sequence</c> (an index). The two differ by one, so no single comparison is correct
    /// for both until those semantics are unified.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Catch_Up_Currently_Never_Completes_And_Leaves_The_Subscription_Out_Of_Sync()
    {
        // arrange
        await using var context = await BrokerTestContext.CreateAsync(streamLength: 3,
            configureSubscription: s => s.Spec.Stream = new() { Offset = StreamPosition.StartOfStream },
            waitForStreamInitialization: false);
        await WaitUntilAsync(() => context.Http.AttemptCount >= 3, "the three missed events to be replayed", context);
        await Task.Delay(250);

        // assert - BROKEN BEHAVIOUR. The fix must make this false
        Assert.True(context.Handler.IsOutOfSync, "catch-up completed, so this characterisation test is obsolete: invert it and rename it");

        // act - and because it stays out of sync, every subsequent live event is silently dropped
        context.ResetRecorders();
        await context.Handler.HandleCloudEventAsync(BrokerTestContext.CreateRecord(3));

        // assert - BROKEN BEHAVIOUR. The fix must make this a dispatch
        Assert.Equal(0, context.Http.AttemptCount);
        Assert.Equal(0, context.Patches.Count);
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
