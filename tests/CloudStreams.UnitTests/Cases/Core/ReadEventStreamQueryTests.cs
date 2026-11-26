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

using CloudStreams.Core.Application.Queries.Streams;

namespace CloudStreams.UnitTests.Cases.Core;

/// <summary>
/// Covers the <see cref="ReadCloudEventStreamQueryHandler"/> (#204)
/// </summary>
public class ReadEventStreamQueryTests
{

    static CloudEventRecord Record(ulong offset) => new(
        "cloud-events",
        offset,
        new CloudEventMetadata() { ContextAttributes = new Dictionary<string, object>() { { "id", offset.ToString() }, { "specversion", "1.0" }, { "source", "https://orders.example.com" }, { "type", "orders.created.v1" } } },
        new { offset });

    static ICloudEventStore Store(out List<(PartitionReference? Partition, StreamReadDirection Direction, long Offset, ulong? Length)> calls)
    {
        var recorded = new List<(PartitionReference?, StreamReadDirection, long, ulong?)>();
        calls = recorded;
        var store = Substitute.For<ICloudEventStore>();
        store.ReadAsync(Arg.Any<string?>(), Arg.Any<StreamReadDirection>(), Arg.Any<long>(), Arg.Any<ulong?>(), Arg.Any<CancellationToken>())
            .Returns(call => { recorded.Add((null, call.ArgAt<StreamReadDirection>(1), call.ArgAt<long>(2), call.ArgAt<ulong?>(3))); return new[] { Record(0), Record(1) }.ToAsyncEnumerable(); });
        store.ReadPartitionAsync(Arg.Any<string?>(), Arg.Any<PartitionReference>(), Arg.Any<StreamReadDirection>(), Arg.Any<long>(), Arg.Any<ulong?>(), Arg.Any<CancellationToken>())
            .Returns(call => { recorded.Add((call.ArgAt<PartitionReference>(1), call.ArgAt<StreamReadDirection>(2), call.ArgAt<long>(3), call.ArgAt<ulong?>(4))); return new[] { Record(3) }.ToAsyncEnumerable(); });
        return store;
    }

    static async Task<(IOperationResult<IAsyncEnumerable<object>> Result, List<object> Items)> HandleAsync(ICloudEventStore store, StreamReadOptions options)
    {
        var result = await new ReadCloudEventStreamQueryHandler(store).HandleAsync(new ReadEventStreamQuery(options), default);
        var items = result.Data == null ? [] : await result.Data.ToListAsync();
        return (result, items);
    }

    [Theory]
    [InlineData(StreamReadDirection.Forwards, StreamPosition.StartOfStream)]
    [InlineData(StreamReadDirection.Backwards, StreamPosition.EndOfStream)]
    public async Task Without_An_Offset_Reading_Should_Start_At_The_End_The_Direction_Starts_From(StreamReadDirection direction, long expectedOffset)
    {
        // act
        await HandleAsync(Store(out var calls), new StreamReadOptions(direction));

        // assert
        var call = Assert.Single(calls);
        Assert.Null(call.Partition);
        Assert.Equal(direction, call.Direction);
        Assert.Equal(expectedOffset, call.Offset);
    }

    [Theory]
    [InlineData(500ul, (ulong)StreamReadOptions.MaxLength)]
    [InlineData(0ul, 1ul)]
    [InlineData(7ul, 7ul)]
    public async Task The_Length_Should_Be_Clamped_Between_One_And_The_Maximum(ulong requested, ulong expected)
    {
        // act
        await HandleAsync(Store(out var calls), new StreamReadOptions(StreamReadDirection.Forwards, 3, requested));

        // assert
        var call = Assert.Single(calls);
        Assert.Equal(3, call.Offset);
        Assert.Equal(expected, call.Length);
    }

    [Fact]
    public async Task Reading_A_Partition_Should_Read_Only_That_Partition()
    {
        // arrange
        var partition = new PartitionReference(CloudEventPartitionType.BySubject, "order-42");

        // act
        var (_, items) = await HandleAsync(Store(out var calls), new StreamReadOptions(partition, StreamReadDirection.Backwards));

        // assert
        var call = Assert.Single(calls);
        Assert.Equal(partition, call.Partition);
        Assert.Equal(StreamPosition.EndOfStream, call.Offset);
        Assert.Single(items);
    }

    [Fact]
    public async Task The_Event_Format_Should_Yield_Cloud_Events_And_The_Record_Format_Records()
    {
        // act
        var (_, events) = await HandleAsync(Store(out _), new StreamReadOptions(StreamReadDirection.Forwards, outputFormat: StreamReadOutputFormat.Event));
        var (_, records) = await HandleAsync(Store(out _), new StreamReadOptions(StreamReadDirection.Forwards, outputFormat: StreamReadOutputFormat.Record));

        // assert
        Assert.All(events, e => Assert.IsType<CloudEvent>(e));
        Assert.Equal(["0", "1"], events.Cast<CloudEvent>().Select(e => e.Id));
        Assert.All(records, r => Assert.IsType<CloudEventRecord>(r));
    }

    [Fact]
    public async Task An_Unknown_Direction_Without_An_Offset_Should_Be_Rejected()
    {
        // act
        var (result, _) = await HandleAsync(Store(out var calls), new StreamReadOptions((StreamReadDirection)42));

        // assert
        Assert.Equal(400, result.Status);
        Assert.Empty(calls);
    }

}
