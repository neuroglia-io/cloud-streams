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

using Neuroglia.Data.Expressions.Services;

namespace CloudStreams.UnitTests.Cases.Broker;

/// <summary>
/// Covers paged catch-up, batched offset commits and the evaluation of filters (see #161)
/// </summary>
public class SubscriptionHandlerPerformanceTests
{

    [Fact]
    public async Task Catch_Up_Should_Read_Pages_And_Deliver_Every_Record_Once_In_Order()
    {
        // arrange
        await using var context = await BrokerTestContext.CreateAsync(streamLength: 250,
            configureSubscription: s => s.Spec.Stream = new() { Offset = StreamPosition.StartOfStream },
            configureOptions: o => o.CatchUp.PageSize = 100,
            waitForStreamInitialization: false);

        // act
        await WaitUntilAsync(() => context.Http.AttemptCount >= 250 && !context.Handler.IsOutOfSync, "the missed records to be replayed");

        // assert - the first two pages are full, so the page that follows each of them is read ahead; the third one is not
        Assert.Equal(Enumerable.Range(0, 250).Select(i => i.ToString()), context.Http.Attempts.Select(a => a.Headers[CloudStreamsHeaders.Offset]));
        Assert.Equal([0L, 100L, 200L], context.EventStore.Reads.Where(r => r.Length == 100).Select(r => r.Offset));
    }

    [Fact]
    public async Task Live_Events_Should_Be_Filtered_Once()
    {
        // arrange
        var evaluator = Substitute.For<IExpressionEvaluator>();
        evaluator.EvaluateAsync(Arg.Any<string>(), Arg.Any<object>(), Arg.Any<IDictionary<string, object>?>(), Arg.Any<Type?>(), Arg.Any<CancellationToken>()).Returns(false);
        await using var context = await BrokerTestContext.CreateAsync(expressionEvaluator: evaluator,
            configureSubscription: s => s.Spec.Filter = new() { Type = CloudEventFilterType.Attributes, Attributes = new Dictionary<string, string>() { { CloudEventAttributes.Type, "${ . == \"another.type\" }" } } });

        // act
        context.EventStore.Publish();

        // assert - a record that is filtered out is still observed: the stream's length, and therefore the subscription's lag, account for it
        await WaitUntilAsync(() => context.Metrics.Lag is { Lag: 0 } && evaluator.ReceivedCalls().Any(), "the live event to be filtered out");
        await Task.Delay(100);
        Assert.Single(evaluator.ReceivedCalls());
        Assert.Equal(0, context.Http.AttemptCount);
    }

    [Fact]
    public async Task Batched_Offsets_Should_Be_Committed_Once_The_Batch_Is_Full_And_When_The_Handler_Stops()
    {
        // arrange
        var context = await BrokerTestContext.CreateAsync(configureOptions: o => o.OffsetCommits = new() { BatchSize = 3, Interval = TimeSpan.FromMinutes(1) });

        // act
        for (ulong offset = 0; offset < 7; offset++) await context.Handler.DispatchRecordAsync(BrokerTestContext.CreateRecord(offset));
        var committedWhileRunning = context.Patches.OffsetsWrittenTo().ToList();
        await context.DisposeAsync();

        // assert
        Assert.Equal(7, context.Http.AttemptCount);
        Assert.Equal([3ul, 6ul], committedWhileRunning);
        Assert.Equal([3ul, 6ul, 7ul], context.Patches.OffsetsWrittenTo());
    }

    [Fact]
    public async Task Batched_Offsets_Should_Be_Committed_Once_The_Interval_Has_Elapsed()
    {
        // arrange
        await using var context = await BrokerTestContext.CreateAsync(configureOptions: o => o.OffsetCommits = new() { BatchSize = 100, Interval = TimeSpan.FromMilliseconds(200) });

        // act
        await context.Handler.DispatchRecordAsync(BrokerTestContext.CreateRecord(0));
        await context.Handler.DispatchRecordAsync(BrokerTestContext.CreateRecord(1));
        var committedAtOnce = context.Patches.OffsetsWrittenTo().ToList();

        // assert
        Assert.Empty(committedAtOnce);
        await WaitUntilAsync(() => context.Patches.OffsetsWrittenTo().Any(), "the batched offset to be committed");
        await Task.Delay(300);
        Assert.Equal([2ul], context.Patches.OffsetsWrittenTo());
    }

    [Fact]
    public async Task Batched_Offsets_Should_Be_Committed_Before_A_Delivery_Is_Suspended_For_Its_Acknowledgement()
    {
        // arrange - the subscriber's acknowledgement advances the offset itself, which a later commit of the batch would otherwise overwrite
        await using var context = await BrokerTestContext.CreateAsync(StubHttpMessageHandler.Responds(HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.Accepted),
            configureOptions: o => o.OffsetCommits = new() { BatchSize = 100, Interval = TimeSpan.FromMinutes(1) });

        // act
        for (ulong offset = 0; offset < 3; offset++) await context.Handler.DispatchRecordAsync(BrokerTestContext.CreateRecord(offset));

        // assert
        Assert.Equal([2ul], context.Patches.OffsetsWrittenTo());
        Assert.Equal(2ul, context.Patches.ValuesWrittenTo("/status/subscriber/pendingAck").Single()["offset"]!.GetValue<ulong>());
        var operations = context.Patches.Operations.Select(o => o.Path.ToString()).ToList();
        Assert.True(operations.FindIndex(p => p.StartsWith("/status/stream")) < operations.FindIndex(p => p.StartsWith("/status/subscriber/pendingAck")), context.Patches.Describe());
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
