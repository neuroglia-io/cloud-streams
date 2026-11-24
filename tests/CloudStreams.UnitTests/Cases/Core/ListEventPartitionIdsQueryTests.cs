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

namespace CloudStreams.UnitTests.Cases.Core;

/// <summary>
/// Covers the sorting (issue #20), prefix search and paging of partition ids
/// </summary>
public class ListEventPartitionIdsQueryTests
{

    // the order the projection holds them in, which is the order they were first published in
    static readonly List<string> ProjectedIds = ["https://zeta.com", "https://Beta.com", "https://alpha.com", "https://beta.com", "https://alpha.com/orders", "https://gamma.com"];

    static async Task<PartitionIdCollection> ExecuteAsync(string? prefix = null, int? maxResults = null, string? continuationToken = null, List<string>? ids = null)
    {
        using var eventStore = new InMemoryCloudEventStore() { PartitionIds = ids ?? ProjectedIds };
        var result = await new ListEventPartitionIdsQueryHandler(eventStore).HandleAsync(new ListEventPartitionIdsQuery(CloudEventPartitionType.BySource, prefix, maxResults, continuationToken), CancellationToken.None);
        return result.Data!;
    }

    [Fact]
    public async Task Ids_Should_Be_Sorted_Case_Insensitively_And_Deterministically()
    {
        // act
        var page = await ExecuteAsync();

        // assert - ids that only differ by case are ordered by ordinal, so the order is always the same
        Assert.Equal(["https://alpha.com", "https://alpha.com/orders", "https://Beta.com", "https://beta.com", "https://gamma.com", "https://zeta.com"], page.Items);
        Assert.Null(page.ContinuationToken);
    }

    [Fact]
    public async Task Prefix_Should_Filter_Ids_Case_Insensitively()
    {
        // act
        var page = await ExecuteAsync(prefix: "HTTPS://ALPHA");

        // assert
        Assert.Equal(["https://alpha.com", "https://alpha.com/orders"], page.Items);
    }

    [Fact]
    public async Task Pages_Should_Cover_Every_Id_Exactly_Once()
    {
        // act
        var ids = new List<string>();
        string? continuationToken = null;
        var pages = 0;
        do
        {
            var page = await ExecuteAsync(maxResults: 4, continuationToken: continuationToken);
            ids.AddRange(page.Items);
            continuationToken = page.ContinuationToken;
            pages++;
        }
        while (continuationToken != null);

        // assert
        Assert.Equal(2, pages);
        Assert.Equal((await ExecuteAsync()).Items, ids);
    }

    [Fact]
    public async Task Paging_Should_Resume_Right_After_The_Last_Id_Seen_Even_When_Ids_Are_Added_In_Between()
    {
        // arrange
        var first = await ExecuteAsync(maxResults: 3);

        // act - an id that sorts before the end of the first page is projected before the next page is requested
        var second = await ExecuteAsync(maxResults: 10, continuationToken: first.ContinuationToken, ids: [.. ProjectedIds, "https://aardvark.com"]);

        // assert - the next page starts right after the last id the client has seen, so nothing it has seen is repeated and nothing after it is skipped.
        // An id added before that point is, by design, only seen by the next traversal
        Assert.Equal(["https://alpha.com", "https://alpha.com/orders", "https://Beta.com"], first.Items);
        Assert.Equal(["https://beta.com", "https://gamma.com", "https://zeta.com"], second.Items);
    }

    [Fact]
    public async Task Duplicate_Ids_Should_Be_Returned_Once()
    {
        // act
        var page = await ExecuteAsync(ids: ["https://b.com", "https://a.com", "https://b.com"]);

        // assert
        Assert.Equal(["https://a.com", "https://b.com"], page.Items);
    }

    [Theory]
    [InlineData(0, null)]
    [InlineData(1001, null)]
    [InlineData(10, "not base64url!")]
    public async Task Invalid_Paging_Parameters_Should_Be_Rejected(int maxResults, string? continuationToken)
    {
        // act
        var exception = await Assert.ThrowsAsync<ProblemDetailsException>(() => ExecuteAsync(maxResults: maxResults, continuationToken: continuationToken));

        // assert
        Assert.Equal(400, exception.Problem.Status);
    }

}
