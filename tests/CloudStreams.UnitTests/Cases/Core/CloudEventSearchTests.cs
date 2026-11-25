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
using Microsoft.Extensions.DependencyInjection;
using Neuroglia.Data.Expressions.JQ;
using Neuroglia.Data.Expressions.Services;
using Neuroglia.Serialization;

namespace CloudStreams.UnitTests.Cases.Core;

/// <summary>
/// Covers the search of the cloud event stream (see #223)
/// </summary>
public class CloudEventSearchTests
{

    static readonly IExpressionEvaluator Evaluator = new ServiceCollection().AddLogging().AddSingleton<ISerializerProvider, SerializerProvider>().AddSingleton<ISerializer>(Neuroglia.Serialization.Json.JsonSerializer.Default)
        .AddJQExpressionEvaluator().BuildServiceProvider().GetRequiredService<IExpressionEvaluator>();

    static readonly DateTimeOffset Start = new(2026, 11, 25, 10, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Creates a store whose records alternate between orders and invoices, one second apart, the data of each carrying its index as its total
    /// </summary>
    static InMemoryCloudEventStore Store(int count)
    {
        var store = new InMemoryCloudEventStore().Seed(count);
        foreach (var record in store.Records)
        {
            var index = (int)record.Sequence;
            record.Metadata.ContextAttributes[CloudEventAttributes.Id] = $"event-{index}";
            record.Metadata.ContextAttributes[CloudEventAttributes.Type] = index % 2 == 0 ? "com.example.order.placed" : "com.example.invoice.issued";
            record.Timestamp = Start.AddSeconds(index);
            record.Data = new Dictionary<string, object>() { ["total"] = index };
        }
        return store;
    }

    static Task<IOperationResult<CloudEventSearchResult>> SearchAsync(InMemoryCloudEventStore store, CloudEventSearchOptions options) =>
        new SearchCloudEventsQueryHandler(store, Evaluator).HandleAsync(new SearchCloudEventsQuery(options), CancellationToken.None);

    static string[] Ids(CloudEventSearchResult result) => [.. result.Records.Select(r => r.Metadata.ContextAttributes[CloudEventAttributes.Id].ToString()!)];

    [Fact]
    public async Task Events_Should_Be_Found_By_Id()
    {
        var result = (await SearchAsync(Store(250), new() { Id = "event-201" })).Data!;
        Assert.Equal(["event-201"], Ids(result));
        Assert.Null(result.NextOffset);
        Assert.Equal(250, result.Scanned);
    }

    [Fact]
    public async Task Events_Should_Be_Found_By_Attribute_Patterns_And_Data_Expressions()
    {
        // act
        var result = (await SearchAsync(Store(20), new() { Attributes = new() { ["type"] = "invoice" }, Expression = ".data.total > 10" })).Data!;

        // assert
        Assert.Equal(["event-11", "event-13", "event-15", "event-17", "event-19"], Ids(result));
    }

    [Fact]
    public async Task A_Search_Should_Stop_At_The_Max_Results_And_Tell_Where_To_Continue()
    {
        // arrange
        var store = Store(20);

        // act
        var first = (await SearchAsync(store, new() { Attributes = new() { ["type"] = "order" }, MaxResults = 3 })).Data!;
        var next = (await SearchAsync(store, new() { Attributes = new() { ["type"] = "order" }, MaxResults = 3, Offset = first.NextOffset })).Data!;

        // assert
        Assert.Equal(["event-0", "event-2", "event-4"], Ids(first));
        Assert.Equal(5ul, first.NextOffset);
        Assert.Equal(["event-6", "event-8", "event-10"], Ids(next));
    }

    [Fact]
    public async Task A_Search_Should_Stop_At_The_Max_Scanned_Records()
    {
        var result = (await SearchAsync(Store(50), new() { Id = "event-40", MaxScanned = 10 })).Data!;
        Assert.Empty(result.Records);
        Assert.Equal(10, result.Scanned);
        Assert.Equal(10ul, result.NextOffset);
    }

    [Fact]
    public async Task A_Search_Should_Be_Bounded_By_A_Time_Range()
    {
        // act
        var result = (await SearchAsync(Store(20), new() { From = Start.AddSeconds(5), To = Start.AddSeconds(8) })).Data!;

        // assert - the search ends at the end of the range, which is the end of what it searches
        Assert.Equal(["event-5", "event-6", "event-7", "event-8"], Ids(result));
        Assert.Null(result.NextOffset);
    }

    [Theory]
    [InlineData("maxResults")]
    [InlineData("partition")]
    [InlineData("pattern")]
    [InlineData("expression")]
    public async Task Invalid_Searches_Should_Be_Refused(string invalidity)
    {
        CloudEventSearchOptions options = invalidity switch
        {
            "maxResults" => new() { MaxResults = 1000 },
            "partition" => new() { PartitionType = CloudEventPartitionType.BySource },
            "pattern" => new() { Attributes = new() { ["type"] = "order.(placed" } },
            _ => new() { Expression = ".data | [" }
        };
        if (invalidity == "expression")
        {
            var exception = await Assert.ThrowsAsync<ProblemDetailsException>(() => SearchAsync(Store(5), options));
            Assert.Equal(400, exception.Problem.Status);
        }
        else Assert.Equal(400, (await SearchAsync(Store(5), options)).Status);
    }

}
