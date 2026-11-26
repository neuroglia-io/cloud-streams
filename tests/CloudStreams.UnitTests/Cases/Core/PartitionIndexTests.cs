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

using CloudStreams.Core.Api.Services;
using CloudStreams.Core.Application.Queries.Partitions;
using Neuroglia.Data.Infrastructure.EventSourcing.Services;

namespace CloudStreams.UnitTests.Cases.Core;

/// <summary>
/// Covers the partition index (#185)
/// </summary>
public class PartitionIndexTests
{

    static CloudEventMetadata Metadata(Dictionary<string, object> attributes) => new(new Dictionary<string, object>(attributes)
    {
        { CloudEventAttributes.SpecVersion, "1.0" },
        { CloudEventAttributes.Id, "1" }
    });

    [Fact]
    public void The_Partitions_Of_An_Event_Should_Be_Extracted_From_Its_Metadata()
    {
        // arrange
        var metadata = Metadata(new()
        {
            { CloudEventAttributes.Source, new Uri("https://orders.example.com") },
            { CloudEventAttributes.Type, "order.created" },
            { CloudEventAttributes.Subject, "order-42" },
            { "$correlationId", "correlation" },
            { "$causationId", "causation" }
        });

        // act
        var partitions = CloudEventStore.GetPartitions(metadata).ToList();

        // assert
        Assert.Equal(
        [
            new(CloudEventPartitionType.BySource, "https://orders.example.com"),
            new(CloudEventPartitionType.ByType, "order.created"),
            new(CloudEventPartitionType.BySubject, "order-42"),
            new(CloudEventPartitionType.ByCorrelationId, "correlation"),
            new PartitionReference(CloudEventPartitionType.ByCausationId, "causation")
        ], partitions);
    }

    [Fact]
    public void Missing_Attributes_Should_Not_Yield_Partitions()
    {
        // act
        var partitions = CloudEventStore.GetPartitions(Metadata(new() { { CloudEventAttributes.Source, "https://orders.example.com" }, { CloudEventAttributes.Type, "order.created" } })).ToList();

        // assert - neither a subject, a correlation id nor a causation id
        Assert.Equal([new(CloudEventPartitionType.BySource, "https://orders.example.com"), new PartitionReference(CloudEventPartitionType.ByType, "order.created")], partitions);
    }

    static (CloudEventStore Store, IPartitionIndex Index) CreateStore(Exception? indexFailure = null)
    {
        var eventStore = Substitute.For<IEventStore>();
        eventStore.AppendAsync(Arg.Any<string>(), Arg.Any<IEnumerable<IEventDescriptor>>(), Arg.Any<long?>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(42ul));
        var index = Substitute.For<IPartitionIndex>();
        if (indexFailure != null) index.AddAsync(Arg.Any<string>(), Arg.Any<IEnumerable<PartitionReference>>(), Arg.Any<CancellationToken>()).Returns(Task.FromException(indexFailure));
        return (new CloudEventStore(eventStore, Substitute.For<IProjectionManager>(), Neuroglia.Serialization.Json.JsonSerializer.Default, partitionIndex: index), index);
    }

    static CloudEventDescriptor Descriptor() => new(Metadata(new()
    {
        { CloudEventAttributes.Source, "https://orders.example.com" },
        { CloudEventAttributes.Type, "order.created" }
    }), new { });

    [Fact]
    public async Task Appending_An_Event_Should_Index_Its_Partitions()
    {
        // arrange
        var (store, index) = CreateStore();

        // act
        await store.AppendAsync(Descriptor());

        // assert
        await index.Received(1).AddAsync(Namespace.DefaultNamespaceName, Arg.Is<IEnumerable<PartitionReference>>(p => p!.Count() == 2), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_Failure_To_Index_Partitions_Should_Not_Fail_The_Ingestion()
    {
        // arrange
        var (store, _) = CreateStore(new InvalidOperationException("redis is down"));

        // act
        var record = await store.AppendAsync(Descriptor());

        // assert
        Assert.Equal(42ul, record.Sequence);
    }

    [Fact]
    public async Task The_Query_Should_Read_A_Single_Page_From_The_Index()
    {
        // arrange
        var index = Substitute.For<IPartitionIndex>();
        index.ListAsync(Namespace.DefaultNamespaceName, CloudEventPartitionType.BySource, "ord", "orders-1", 3, Arg.Any<CancellationToken>()).Returns(["orders-2", "orders-3", "orders-4"]);
        var handler = new ListEventPartitionIdsQueryHandler(Substitute.For<ICloudEventStore>(), index);
        var token = System.Buffers.Text.Base64Url.EncodeToString(System.Text.Encoding.UTF8.GetBytes("orders-1"));

        // act
        var result = await handler.HandleAsync(new ListEventPartitionIdsQuery(CloudEventPartitionType.BySource, "ord", 2, token), CancellationToken.None);

        // assert - one more id than requested is read, to know whether there is a next page
        Assert.Equal(["orders-2", "orders-3"], result.Data!.Items);
        Assert.NotNull(result.Data.ContinuationToken);
    }

    [Theory]
    [InlineData("orders", "ORDERS\0orders")]
    [InlineData("Orders", "ORDERS\0Orders")]
    public void Members_Should_Sort_Case_Insensitively_First_And_Exactly_Second(string id, string member)
    {
        Assert.Equal(member, RedisPartitionIndex.ToMember(id));
        Assert.True(string.CompareOrdinal(RedisPartitionIndex.ToMember("Orders"), RedisPartitionIndex.ToMember("orders")) < 0);
        Assert.True(PartitionIdCollection.Comparer.Compare("Orders", "orders") < 0);
    }

}
