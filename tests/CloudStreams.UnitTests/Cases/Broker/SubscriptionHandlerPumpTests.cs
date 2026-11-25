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

using System.Collections.Concurrent;
using System.Text.Json;

namespace CloudStreams.UnitTests.Cases.Broker;

/// <summary>
/// Covers the delivery of cloud events in batches, and in parallel in order per key (see #212)
/// </summary>
public class SubscriptionHandlerPumpTests
{

    static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    static HttpClientRetryPolicy NoRetry => new() { MaxAttempts = 0, BackoffDuration = RetryBackoffDuration.Constant(Duration.FromSeconds(0)) };

    /// <summary>
    /// Records what a subscriber receives: the ids of the cloud events of each request, and whether it was a batch
    /// </summary>
    sealed class Recorder
    {

        public ConcurrentQueue<(bool Batch, string[] Ids, string Offset)> Requests { get; } = new();

        public ConcurrentQueue<string> Ids { get; } = new();

        public async Task<HttpResponseMessage> RecordAsync(HttpRequestMessage request, Func<bool, string[], HttpStatusCode>? respond = null)
        {
            var batch = request.Content!.Headers.ContentType!.MediaType == CloudStreams.Core.CloudEventExtensions.BatchContentType;
            var json = JsonDocument.Parse(await request.Content.ReadAsStringAsync());
            string[] ids = batch ? [.. json.RootElement.EnumerateArray().Select(e => e.GetProperty("id").GetString()!)] : [json.RootElement.GetProperty("id").GetString()!];
            var status = respond?.Invoke(batch, ids) ?? HttpStatusCode.OK;
            if ((int)status < 300)
            {
                this.Requests.Enqueue((batch, ids, request.Headers.GetValues(CloudStreamsHeaders.Offset).Single()));
                foreach (var id in ids) this.Ids.Enqueue(id);
            }
            return new HttpResponseMessage(status);
        }

    }

    static async Task WaitUntilAsync(Func<bool> condition, string description)
    {
        var deadline = DateTime.UtcNow + Timeout;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException($"Timed out waiting until {description}");
            await Task.Delay(10);
        }
    }

    static string[] IdsOf(BrokerTestContext context) => [.. context.EventStore.Records.Select(r => r.Metadata.ContextAttributes[CloudEventAttributes.Id].ToString()!)];

    [Fact]
    public async Task Events_Should_Be_Delivered_In_Batches_And_Committed_Once_Per_Batch()
    {
        // arrange
        var recorder = new Recorder();
        await using var context = await BrokerTestContext.CreateAsync(new((request, _, _) => recorder.RecordAsync(request)), streamLength: 5, waitForStreamInitialization: false, configureSubscription: s =>
        {
            s.Spec.Stream = new() { Offset = 0 };
            s.Spec.Subscriber.Batch = new() { MaxSize = 2 };
        });

        // act
        await WaitUntilAsync(() => recorder.Ids.Count >= 5, "every event is delivered");
        await WaitUntilAsync(() => context.Patches.OffsetsWrittenTo().Contains(5ul), "the last batch is committed");

        // assert
        Assert.Equal(IdsOf(context), recorder.Ids);
        Assert.Equal([2, 2, 1], recorder.Requests.Select(r => r.Ids.Length));
        Assert.All(recorder.Requests, r => Assert.True(r.Batch));
        Assert.Equal(["0", "2", "4"], recorder.Requests.Select(r => r.Offset));
        Assert.Equal([2ul, 4ul, 5ul], context.Patches.OffsetsWrittenTo());
        Assert.Equal(5, context.Metrics.TotalPublishedEvents);
    }

    [Fact]
    public async Task Live_Events_Should_Be_Delivered_In_Batches()
    {
        // arrange
        var recorder = new Recorder();
        await using var context = await BrokerTestContext.CreateAsync(new((request, _, _) => recorder.RecordAsync(request)), configureSubscription: s => s.Spec.Subscriber.Batch = new() { MaxSize = 10 });

        // act
        for (var i = 0; i < 3; i++) context.EventStore.Publish();
        await WaitUntilAsync(() => recorder.Ids.Count >= 3, "the live events are delivered");

        // assert
        Assert.Equal(IdsOf(context), recorder.Ids);
        Assert.All(recorder.Requests, r => Assert.True(r.Batch));
    }

    [Fact]
    public async Task A_Partial_Batch_Should_Linger_For_More_Events()
    {
        // arrange
        var recorder = new Recorder();
        await using var context = await BrokerTestContext.CreateAsync(new((request, _, _) => recorder.RecordAsync(request)), configureSubscription: s => s.Spec.Subscriber.Batch = new() { MaxSize = 3, Linger = Duration.FromMilliseconds(400) });

        // act - the second event is published while the first one lingers
        context.EventStore.Publish();
        await Task.Delay(100);
        context.EventStore.Publish();
        await WaitUntilAsync(() => recorder.Ids.Count >= 2, "the events are delivered");

        // assert
        Assert.Equal(2, Assert.Single(recorder.Requests).Ids.Length);
    }

    [Fact]
    public async Task A_Batch_That_Fails_For_Good_Should_Be_Delivered_One_Event_At_A_Time()
    {
        // arrange - the subscriber refuses batches that contain the event at offset 1, which is dead-lettered once isolated
        var recorder = new Recorder();
        string? poison = null;
        await using var context = await BrokerTestContext.CreateAsync(new((request, _, _) => recorder.RecordAsync(request, (batch, ids) => ids.Contains(poison) ? HttpStatusCode.BadRequest : HttpStatusCode.OK)),
            streamLength: 3, waitForStreamInitialization: false, configureEventStore: store => poison = store.Records[1].Metadata.ContextAttributes[CloudEventAttributes.Id].ToString(), configureSubscription: s =>
            {
                s.Spec.Stream = new() { Offset = 0 };
                s.Spec.Subscriber.Batch = new() { MaxSize = 3 };
                s.Spec.Subscriber.RetryPolicy = NoRetry;
                s.Spec.DeadLetter = new();
            });

        // act
        await WaitUntilAsync(() => context.Patches.OffsetsWrittenTo().Contains(3ul), "the batch is committed");

        // assert
        var ids = IdsOf(context);
        Assert.Equal([ids[0], ids[2]], recorder.Ids);
        Assert.All(recorder.Requests, r => Assert.False(r.Batch));
        Assert.Equal(1ul, Assert.Single(context.DeadLetters.All).EventOffset);
        Assert.False(context.Patches.Touched("/status/stream/fault"));
    }

    [Fact]
    public async Task Events_Should_Be_Delivered_In_Order_Per_Key_And_In_Parallel_Across_Keys()
    {
        // arrange - the events of key 'a' are slow to deliver: those of key 'b' are not held back by them
        var recorder = new Recorder();
        var store = (InMemoryCloudEventStore?)null;
        await using var context = await BrokerTestContext.CreateAsync(new(async (request, _, cancellationToken) =>
        {
            var subject = await ReadSubjectAsync(request);
            if (subject == "a") await Task.Delay(150, cancellationToken);
            return await recorder.RecordAsync(request);
        }), waitForStreamInitialization: false, configureEventStore: s => (store = s).SeedWithSubjects("a", "b", "a", "b", "a", "b"), configureSubscription: s =>
        {
            s.Spec.Stream = new() { Offset = 0 };
            s.Spec.Subscriber.Ordering = new() { Key = CloudEventAttributes.Subject, MaxConcurrency = 2 };
        });

        // act
        await WaitUntilAsync(() => recorder.Ids.Count >= 6, "every event is delivered");
        await WaitUntilAsync(() => context.Patches.OffsetsWrittenTo().Contains(6ul), "the page is committed");

        // assert - each key's events are delivered in the order of the stream, and every event of 'b' before the last one of 'a'
        var ids = IdsOf(context);
        var delivered = recorder.Ids.ToList();
        Assert.Equal([ids[0], ids[2], ids[4]], delivered.Where(id => Array.IndexOf(ids, id) % 2 == 0));
        Assert.Equal([ids[1], ids[3], ids[5]], delivered.Where(id => Array.IndexOf(ids, id) % 2 == 1));
        Assert.True(delivered.IndexOf(ids[5]) < delivered.IndexOf(ids[4]), $"Expected the events of key 'b' not to wait for those of key 'a', got {string.Join(", ", delivered.Select(id => Array.IndexOf(ids, id)))}");
        // the offset is only committed once the whole page has been delivered
        Assert.Equal([6ul], context.Patches.OffsetsWrittenTo());
    }

    [Fact]
    public async Task A_Failure_On_One_Key_Should_Not_Commit_The_Page()
    {
        // arrange - the subscriber refuses the events of key 'a' for good
        await using var context = await BrokerTestContext.CreateAsync(new(async (request, _, _) => new HttpResponseMessage(await ReadSubjectAsync(request) == "a" ? HttpStatusCode.BadRequest : HttpStatusCode.OK)),
            waitForStreamInitialization: false, configureEventStore: s => s.SeedWithSubjects("b", "a", "b"), configureSubscription: s =>
            {
                s.Spec.Stream = new() { Offset = 0 };
                s.Spec.Subscriber.Ordering = new() { Key = CloudEventAttributes.Subject };
                s.Spec.Subscriber.RetryPolicy = NoRetry;
            });

        // act
        await WaitUntilAsync(() => context.Patches.Touched("/status/stream/fault"), "the subscription is faulted");
        await Task.Delay(200);

        // assert - the page is redelivered once the subscription is resumed: the offset must not move past the event that faulted it
        Assert.Empty(context.Patches.OffsetsWrittenTo());
    }

    [Fact]
    public async Task A_Subscriber_Accepting_A_Delivery_Should_Not_Defer_Its_Acknowledgement()
    {
        // arrange
        await using var context = await BrokerTestContext.CreateAsync(StubHttpMessageHandler.AlwaysResponds(HttpStatusCode.Accepted), streamLength: 2, waitForStreamInitialization: false, configureSubscription: s =>
        {
            s.Spec.Stream = new() { Offset = 0 };
            s.Spec.Subscriber.Ordering = new() { Key = CloudEventAttributes.Subject };
        });

        // act
        await WaitUntilAsync(() => context.Patches.OffsetsWrittenTo().Contains(2ul), "the page is committed");

        // assert
        Assert.False(context.Patches.Touched("/status/subscriber/pendingAck"));
        Assert.All(context.Http.Attempts, a => Assert.False(a.Headers.ContainsKey(CloudStreamsHeaders.AckToken)));
    }

    static async Task<string?> ReadSubjectAsync(HttpRequestMessage request)
    {
        var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
        return json.RootElement.TryGetProperty("subject", out var subject) ? subject.GetString() : null;
    }

}
