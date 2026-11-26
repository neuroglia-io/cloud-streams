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

using CloudStreams.Core.Application.Queries.Partitions;
using CloudStreams.Core.Application.Queries.Streams;
using CloudStreams.Core.Application.Queries.Subscriptions;
using Microsoft.Extensions.DependencyInjection;
using Neuroglia.Data.Expressions.JQ;
using Neuroglia.Data.Expressions.Services;
using Neuroglia.Serialization;

namespace CloudStreams.UnitTests.Cases.Core;

/// <summary>
/// Covers the queries behind the dashboard's insight pages: the subscription playground (#224), the activity of the stream (#225) and the partitions browser (#226)
/// </summary>
public class InsightQueryTests
{

    static readonly IExpressionEvaluator ExpressionEvaluator = new ServiceCollection().AddLogging().AddSingleton<ISerializerProvider, SerializerProvider>()
        .AddSingleton<ISerializer>(Neuroglia.Serialization.Json.JsonSerializer.Default).AddJQExpressionEvaluator().BuildServiceProvider().GetRequiredService<IExpressionEvaluator>();

    static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    static CloudEventRecord Record(ulong sequence, string type, string source = "https://orders.example.com", DateTimeOffset? timestamp = null)
    {
        var attributes = new Dictionary<string, object>() { ["specversion"] = "1.0", ["id"] = Guid.NewGuid().ToString(), ["type"] = type, ["source"] = source };
        return new("cloud-events", sequence, new(attributes), new Dictionary<string, object>() { ["total"] = 42 }) { Timestamp = timestamp };
    }

    static CloudEvent Event(string type) => new() { Id = Guid.NewGuid().ToString(), Source = new("https://orders.example.com"), Type = type, Data = new Dictionary<string, object>() { ["total"] = 42 } };

    static TestSubscriptionQueryHandler Tester(ICloudEventStore? eventStore = null) => new(eventStore ?? Substitute.For<ICloudEventStore>(), ExpressionEvaluator, [], Neuroglia.Serialization.Json.JsonSerializer.Default);

    /// <summary>
    /// Stands for an event store holding the specified records, which it reads like EventStoreDB does
    /// </summary>
    static ICloudEventStore Store(IReadOnlyList<CloudEventRecord> records)
    {
        var store = Substitute.For<ICloudEventStore>();
        store.GetStreamMetadataAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(new StreamMetadata() { Length = (ulong)records.Count });
        store.ReadAsync(Arg.Any<string?>(), Arg.Any<StreamReadDirection>(), Arg.Any<long>(), Arg.Any<ulong?>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var direction = call.ArgAt<StreamReadDirection>(1);
            var offset = call.ArgAt<long>(2);
            var length = (int)Math.Min(call.ArgAt<ulong?>(3) ?? ulong.MaxValue, int.MaxValue);
            IEnumerable<CloudEventRecord> selected = direction == StreamReadDirection.Forwards
                ? records.Skip((int)offset)
                : records.Take(offset == StreamPosition.EndOfStream ? records.Count : (int)offset + 1).Reverse();
            return selected.Take(length).ToAsyncEnumerable();
        });
        return store;
    }

    [Fact]
    public async Task Testing_Sample_Events_Should_Tell_Which_Ones_Match_And_What_They_Become()
    {
        // arrange
        var request = new SubscriptionTestRequest()
        {
            Filter = new() { Type = CloudEventFilterType.Attributes, Attributes = new Dictionary<string, string>() { ["type"] = "order\\..*" } },
            Mutation = new() { Type = CloudEventMutationType.Expression, Expression = "${ . + { subject: \"mutated\" } }" },
            Events = [Event("order.placed"), Event("invoice.sent")]
        };

        // act
        var result = await Tester().HandleAsync(new TestSubscriptionQuery(request), CancellationToken.None);

        // assert
        Assert.Equal(200, result.Status);
        Assert.Collection(result.Data!.Items,
            placed =>
            {
                Assert.True(placed.Matched);
                Assert.Null(placed.Offset);
                Assert.Equal("mutated", placed.Mutated!.Subject);
                Assert.Null(placed.Error);
            },
            sent =>
            {
                Assert.False(sent.Matched);
                Assert.Null(sent.Mutated);
            });
        Assert.Null(result.Data.Notice);
    }

    [Fact]
    public async Task Testing_Should_Read_The_Most_Recent_Events_When_There_Is_No_Sample()
    {
        // arrange
        var records = Enumerable.Range(0, 30).Select(i => Record((ulong)i, i % 2 == 0 ? "order.placed" : "invoice.sent")).ToList();
        var request = new SubscriptionTestRequest() { Filter = new() { Type = CloudEventFilterType.Expression, Expression = "${ .metadata.contextAttributes.type == \"order.placed\" }" }, Length = 4 };

        // act
        var result = await Tester(Store(records)).HandleAsync(new TestSubscriptionQuery(request), CancellationToken.None);

        // assert - most recent first, with their offsets
        Assert.Equal([29ul, 28ul, 27ul, 26ul], result.Data!.Items.Select(i => i.Offset!.Value));
        Assert.Equal([false, true, false, true], result.Data.Items.Select(i => i.Matched));
    }

    [Fact]
    public async Task An_Expression_That_Fails_Should_Be_Reported_With_The_Event_It_Failed_For()
    {
        // arrange
        var request = new SubscriptionTestRequest() { Filter = new() { Type = CloudEventFilterType.Expression, Expression = "${ .data.total | error(\"boom\") }" }, Events = [Event("order.placed")] };

        // act
        var result = await Tester().HandleAsync(new TestSubscriptionQuery(request), CancellationToken.None);

        // assert
        Assert.Equal(200, result.Status);
        Assert.NotNull(Assert.Single(result.Data!.Items).Error);
    }

    [Fact]
    public async Task A_Webhook_Mutation_Should_Not_Be_Called()
    {
        // arrange
        var request = new SubscriptionTestRequest() { Mutation = new() { Type = CloudEventMutationType.Webhook, Webhook = new() { ServiceUri = new("http://localhost:1/mutate") } }, Events = [Event("order.placed")] };

        // act
        var result = await Tester().HandleAsync(new TestSubscriptionQuery(request), CancellationToken.None);

        // assert
        Assert.Equal(TestSubscriptionQueryHandler.WebhookNotice, result.Data!.Notice);
        var item = Assert.Single(result.Data.Items);
        Assert.True(item.Matched);
        Assert.Null(item.Mutated);
        Assert.Null(item.Error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(SubscriptionTestRequest.MaxLength + 1)]
    public async Task Testing_Too_Few_Or_Too_Many_Events_Should_Be_Refused(int length)
    {
        // act
        var result = await Tester().HandleAsync(new TestSubscriptionQuery(new() { Length = length }), CancellationToken.None);

        // assert
        Assert.Equal(400, result.Status);
    }

    [Fact]
    public async Task The_Activity_Should_Count_The_Events_Recorded_In_Each_Bucket()
    {
        // arrange - 3 events 5 hours ago, none 4 hours ago, then 1, 2 and 4 per hour
        var hoursAgo = new[] { 4.5, 4.5, 4.5, 2.5, 1.5, 1.5, 0.5, 0.5, 0.5, 0.5 };
        var records = hoursAgo.Select((h, i) => Record((ulong)i, i < 3 ? "order.placed" : "order.shipped", i < 7 ? "https://orders.example.com" : "https://shipping.example.com", Now.AddHours(-h))).ToList();

        // act
        var result = await new GetEventStreamActivityQueryHandler(Store(records)).HandleAsync(new GetEventStreamActivityQuery(TimeSpan.FromHours(5), 5, 100), CancellationToken.None);

        // assert
        Assert.Equal(200, result.Status);
        Assert.Equal([3ul, 0ul, 1ul, 2ul, 4ul], result.Data!.Buckets.Select(b => b.Count));
        Assert.Equal(10, result.Data.SampleSize);
        Assert.Equal(("order.shipped", 7), (result.Data.Types[0].Value, result.Data.Types[0].Count));
        Assert.Equal(("https://orders.example.com", 7), (result.Data.Sources[0].Value, result.Data.Sources[0].Count));
        Assert.Equal(2, result.Data.Sources.Count);
    }

    [Fact]
    public async Task The_Activity_Of_An_Empty_Stream_Should_Be_Empty_Buckets()
    {
        // act
        var result = await new GetEventStreamActivityQueryHandler(Store([])).HandleAsync(new GetEventStreamActivityQuery(TimeSpan.FromHours(24), 24, 100), CancellationToken.None);

        // assert
        Assert.Equal(24, result.Data!.Buckets.Count);
        Assert.All(result.Data.Buckets, b => Assert.Equal(0ul, b.Count));
        Assert.Empty(result.Data.Sources);
    }

    [Theory]
    [InlineData(0, 24)]
    [InlineData(24, 0)]
    [InlineData(24, StreamActivity.MaxBuckets + 1)]
    [InlineData(24 * 32, 24)]
    public async Task An_Invalid_Activity_Request_Should_Be_Refused(int hours, int buckets)
    {
        // act
        var result = await new GetEventStreamActivityQueryHandler(Store([])).HandleAsync(new GetEventStreamActivityQuery(TimeSpan.FromHours(hours), buckets, 100), CancellationToken.None);

        // assert
        Assert.Equal(400, result.Status);
    }

    [Fact]
    public async Task Partitions_Should_Be_Listed_With_Their_Metadata_Page_By_Page()
    {
        // arrange
        var store = Substitute.For<ICloudEventStore>();
        store.ListPartitionIdsAsync(CloudEventPartitionType.BySource, Arg.Any<CancellationToken>()).Returns(new[] { "c", "a", "b", "pending" }.ToAsyncEnumerable());
        store.GetPartitionMetadataAsync(Arg.Any<string?>(), Arg.Any<PartitionReference>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var partition = call.Arg<PartitionReference>();
            if (partition.Id == "pending") throw new StreamNotFoundException(partition.Id);
            return new PartitionMetadata() { Type = partition.Type, Id = partition.Id, Length = (ulong)partition.Id[0], FirstEvent = Now.AddDays(-1), LastEvent = Now };
        });
        var handler = new ListEventPartitionsQueryHandler(store);

        // act
        var first = await handler.HandleAsync(new ListEventPartitionsQuery(CloudEventPartitionType.BySource, maxResults: 2), CancellationToken.None);
        var second = await handler.HandleAsync(new ListEventPartitionsQuery(CloudEventPartitionType.BySource, maxResults: 2, continuationToken: first.Data!.ContinuationToken), CancellationToken.None);

        // assert
        Assert.Equal(["a", "b"], first.Data.Items.Select(p => p.Id));
        Assert.Equal((ulong)'a', first.Data.Items[0].Length);
        Assert.NotNull(first.Data.ContinuationToken);
        Assert.Equal(["c", "pending"], second.Data!.Items.Select(p => p.Id));
        Assert.Equal(default, second.Data.Items[1].FirstEvent);
        Assert.Null(second.Data.ContinuationToken);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(PartitionMetadataCollection.MaxResultsLimit + 1)]
    public async Task Listing_Too_Few_Or_Too_Many_Partitions_Should_Be_Refused(int maxResults)
    {
        // act
        var result = await new ListEventPartitionsQueryHandler(Substitute.For<ICloudEventStore>()).HandleAsync(new ListEventPartitionsQuery(CloudEventPartitionType.BySource, maxResults: maxResults), CancellationToken.None);

        // assert
        Assert.Equal(400, result.Status);
    }

}
