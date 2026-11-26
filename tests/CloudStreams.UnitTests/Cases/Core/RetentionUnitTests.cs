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

using CloudStreams.Core.Api.Configuration;
using CloudStreams.Core.Api.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Neuroglia.Data.Infrastructure.EventSourcing.Services;
using System.Runtime.CompilerServices;

namespace CloudStreams.UnitTests.Cases.Core;

/// <summary>
/// Covers the retention policy of the cloud event stream: its configuration, how it is applied, and how partitions are read once their oldest events expired (see #206)
/// </summary>
public class RetentionUnitTests
{

    const string PartitionStream = "$by-source-default/https://retention.cloud-streams.io";

    static readonly PartitionReference Partition = new(CloudEventPartitionType.BySource, "https://retention.cloud-streams.io");

    /// <summary>
    /// Stands for the underlying event store, whose partition stream links to 10 events, the first ones of which have expired
    /// </summary>
    sealed class ExpiringLinks
    {

        public ExpiringLinks(int expired)
        {
            this.Expired = expired;
            this.EventStore.ReadAsync(PartitionStream, Arg.Any<StreamReadDirection>(), Arg.Any<long>(), Arg.Any<ulong?>(), Arg.Any<CancellationToken>())
                .Returns(call =>
                {
                    lock (this.Reads) this.Reads.Add(call.ArgAt<long>(2));
                    return this.ReadAsync(call.ArgAt<StreamReadDirection>(1), call.ArgAt<long>(2), call.ArgAt<ulong?>(3));
                });
        }

        public IEventStore EventStore { get; } = Substitute.For<IEventStore>();

        public int Expired { get; set; }

        public List<long> Reads { get; } = [];

        async IAsyncEnumerable<IEventRecord> ReadAsync(StreamReadDirection direction, long offset, ulong? length)
        {
            await Task.Yield();
            var offsets = direction == StreamReadDirection.Forwards
                ? Enumerable.Range((int)Math.Max(offset, 0), Math.Max(0, 10 - (int)Math.Max(offset, 0)))
                : Enumerable.Range(0, offset == StreamPosition.EndOfStream ? 10 : (int)offset + 1).Reverse();
            foreach (var position in offsets.Take((int)Math.Min(length ?? int.MaxValue, int.MaxValue)))
            {
                if (position < this.Expired) DeserializeEventRecord();
                var record = Substitute.For<IEventRecord>();
                record.StreamId.Returns(PartitionStream);
                record.Offset.Returns((ulong)position);
                record.Timestamp.Returns(DateTimeOffset.UtcNow.AddMinutes(position - 10));
                record.Metadata.Returns(new Dictionary<string, object>() { ["type"] = "io.cloud-streams.test.v1" });
                yield return record;
            }
        }

        /// <summary>
        /// Fails like the underlying event store does when it reads a link whose event has expired
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        static void DeserializeEventRecord() => throw new NullReferenceException();

        public CloudEventStore CreateStore() => new(this.EventStore, Substitute.For<IProjectionManager>(), Neuroglia.Serialization.Json.JsonSerializer.Default);

    }

    [Fact]
    public async Task Reading_A_Partition_Forwards_Should_Skip_The_Links_To_Expired_Events()
    {
        // arrange
        var links = new ExpiringLinks(6);
        var store = links.CreateStore();

        // act
        var first = await store.ReadPartitionAsync(null, Partition, StreamReadDirection.Forwards, StreamPosition.StartOfStream).Select(r => r.Sequence).ToListAsync();
        links.Reads.Clear();
        var second = await store.ReadPartitionAsync(null, Partition, StreamReadDirection.Forwards, 2, 2).Select(r => r.Sequence).ToListAsync();

        // assert - the first retained link is found once, and starts the next reads
        Assert.Equal([6ul, 7ul, 8ul, 9ul], first);
        Assert.Equal([6ul, 7ul], second);
        Assert.Equal([6L], links.Reads);
    }

    [Fact]
    public async Task Reading_A_Partition_Backwards_Should_Stop_At_The_First_Link_To_An_Expired_Event()
    {
        // arrange
        var store = new ExpiringLinks(6).CreateStore();

        // act
        var records = await store.ReadPartitionAsync(null, Partition, StreamReadDirection.Backwards, StreamPosition.EndOfStream).Select(r => r.Sequence).ToListAsync();
        var expired = await store.ReadPartitionAsync(null, Partition, StreamReadDirection.Backwards, 3).ToListAsync();

        // assert
        Assert.Equal([9ul, 8ul, 7ul, 6ul], records);
        Assert.Empty(expired);
    }

    [Fact]
    public async Task The_Metadata_Of_A_Partition_Should_Start_With_Its_First_Retained_Event()
    {
        // act
        var metadata = await new ExpiringLinks(6).CreateStore().GetPartitionMetadataAsync(null, Partition);

        // assert
        Assert.Equal(10ul, metadata.Length);
        Assert.Equal(6ul, metadata.FirstOffset);
    }

    [Fact]
    public async Task A_Partition_Whose_Events_All_Expired_Should_Be_Reported_As_Such()
    {
        // arrange
        var store = new ExpiringLinks(10).CreateStore();

        // act
        var records = await store.ReadPartitionAsync(null, Partition, StreamReadDirection.Forwards, StreamPosition.StartOfStream).ToListAsync();
        var exception = await Assert.ThrowsAsync<ProblemDetailsException>(() => store.GetPartitionMetadataAsync(null, Partition));

        // assert
        Assert.Empty(records);
        Assert.Equal((int)HttpStatusCode.NotFound, exception.Problem.Status);
        Assert.Contains("expired", exception.Problem.Detail);
    }

    [Fact]
    public async Task Other_Failures_Should_Not_Be_Mistaken_For_Expired_Links()
    {
        // arrange
        var eventStore = Substitute.For<IEventStore>();
        eventStore.ReadAsync(Arg.Any<string>(), Arg.Any<StreamReadDirection>(), Arg.Any<long>(), Arg.Any<ulong?>(), Arg.Any<CancellationToken>()).Returns(_ => Fails());
        var store = new CloudEventStore(eventStore, Substitute.For<IProjectionManager>(), Neuroglia.Serialization.Json.JsonSerializer.Default);

        // act & assert
        await Assert.ThrowsAsync<NullReferenceException>(() => store.ReadPartitionAsync(null, Partition, StreamReadDirection.Forwards, StreamPosition.StartOfStream).ToListAsync().AsTask());

        static async IAsyncEnumerable<IEventRecord> Fails()
        {
            await Task.Yield();
            throw new NullReferenceException();
#pragma warning disable CS0162 // makes the method an async iterator
            yield break;
#pragma warning restore CS0162
        }
    }

    [Theory]
    [InlineData(null, null, false)]
    [InlineData("P30D", null, true)]
    [InlineData(null, 1000ul, true)]
    [InlineData("PT0S", 0ul, true)]
    public void The_Configured_Policy_Should_Only_Exist_When_Configured(string? maxAge, ulong? maxCount, bool configured)
    {
        // act
        var policy = new RetentionOptions() { MaxAge = maxAge, MaxCount = maxCount }.GetPolicy();

        // assert
        Assert.Equal(configured, policy != null);
        if (maxAge == "P30D") Assert.Equal(TimeSpan.FromDays(30), policy!.MaxAge!.ToTimeSpan());
        if (maxAge == "PT0S") Assert.True(policy!.IsUnlimited);
    }

    [Theory]
    [InlineData("thirty days", null)]
    [InlineData("PT0.5S", null)]
    [InlineData(null, 3_000_000_000ul)]
    public void An_Invalid_Policy_Should_Be_Refused(string? maxAge, ulong? maxCount)
    {
        Assert.Throws<FormatException>(() => new RetentionOptions() { MaxAge = maxAge, MaxCount = maxCount }.GetPolicy());
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData(500ul, true)]
    [InlineData(1000ul, false)]
    public async Task The_Configured_Policy_Should_Be_Applied_Unless_It_Is_Already(ulong? currentMaxCount, bool applied)
    {
        // arrange
        var store = Substitute.For<IStreamRetentionStore>();
        store.GetAsync(Arg.Any<CancellationToken>()).Returns(currentMaxCount.HasValue ? new StreamRetention() { MaxCount = currentMaxCount } : null);
        var initializer = new StreamRetentionInitializer(NullLogger<StreamRetentionInitializer>.Instance, Microsoft.Extensions.Options.Options.Create(new RetentionOptions() { MaxCount = 1000 }), store);

        // act
        await initializer.StartAsync(CancellationToken.None);
        await initializer.ExecuteTask!;

        // assert
        await store.Received(applied ? 1 : 0).SetAsync(Arg.Is<StreamRetention>(r => r!.MaxCount == 1000), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Without_A_Configured_Policy_The_Current_One_Should_Be_Left_As_It_Is()
    {
        // arrange
        var store = Substitute.For<IStreamRetentionStore>();
        var initializer = new StreamRetentionInitializer(NullLogger<StreamRetentionInitializer>.Instance, Microsoft.Extensions.Options.Options.Create(new RetentionOptions()), store);

        // act
        await initializer.StartAsync(CancellationToken.None);
        await initializer.ExecuteTask!;

        // assert
        await store.DidNotReceiveWithAnyArgs().GetAsync(default);
        await store.DidNotReceiveWithAnyArgs().SetAsync(default!, default);
    }

}
