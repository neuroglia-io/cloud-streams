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

using CloudStreams.Core.Application.Services;
using System.Buffers.Text;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace CloudStreams.Core.Application.Queries.Partitions;

/// <summary>
/// Represents the query used to list, page by page, the ids of the cloud event partitions of the specified type, sorted alphabetically
/// </summary>
/// <param name="partitionType">The type of the partitions to list the ids of</param>
/// <param name="prefix">The prefix, matched case-insensitively, the ids to list must start with, if any</param>
/// <param name="maxResults">The maximum amount of ids to return. Defaults to <see cref="PartitionIdCollection.DefaultMaxResults"/></param>
/// <param name="continuationToken">The token returned with the previous page, used to get the next one, if any</param>
public class ListEventPartitionIdsQuery(CloudEventPartitionType partitionType, string? prefix = null, int? maxResults = null, string? continuationToken = null)
    : Query<PartitionIdCollection>
{

    /// <summary>
    /// Gets the type of the partitions to list the ids of
    /// </summary>
    [Required]
    public virtual CloudEventPartitionType PartitionType { get; set; } = partitionType;

    /// <summary>
    /// Gets the prefix, matched case-insensitively, the ids to list must start with, if any
    /// </summary>
    public virtual string? Prefix { get; set; } = prefix;

    /// <summary>
    /// Gets the maximum amount of ids to return, if any
    /// </summary>
    public virtual int? MaxResults { get; set; } = maxResults;

    /// <summary>
    /// Gets the token returned with the previous page, used to get the next one, if any
    /// </summary>
    public virtual string? ContinuationToken { get; set; } = continuationToken;

}

/// <summary>
/// Represents the service used to handle <see cref="ListEventPartitionIdsQuery"/> instances
/// </summary>
/// <param name="eventStore">The service used to store cloud events</param>
/// <remarks>
/// Ids are sorted when they are read rather than when they are projected, so that the order applies to every deployment without resetting its projections,
/// and is the same for every client (see #20). The whole list of ids is held by the projection's state anyway, so sorting it costs little compared to reading it.
/// Pages are keyed by the last id they contain, so a page never skips nor repeats an id because another one has been added in the meantime
/// </remarks>
public class ListEventPartitionIdsQueryHandler(ICloudEventStore eventStore)
    : IQueryHandler<ListEventPartitionIdsQuery, PartitionIdCollection>
{

    /// <inheritdoc/>
    public virtual async Task<IOperationResult<PartitionIdCollection>> HandleAsync(ListEventPartitionIdsQuery query, CancellationToken cancellationToken)
    {
        var maxResults = query.MaxResults ?? PartitionIdCollection.DefaultMaxResults;
        if (maxResults < 1 || maxResults > PartitionIdCollection.MaxResultsLimit) throw BadRequest($"The maximum amount of results must be between 1 and {PartitionIdCollection.MaxResultsLimit}");
        var after = string.IsNullOrWhiteSpace(query.ContinuationToken) ? null : DecodeContinuationToken(query.ContinuationToken);
        IEnumerable<string> ids = (await eventStore.ListPartitionIdsAsync(query.PartitionType, cancellationToken).ToListAsync(cancellationToken).ConfigureAwait(false)).Distinct(StringComparer.Ordinal);
        if (!string.IsNullOrEmpty(query.Prefix)) ids = ids.Where(id => id.StartsWith(query.Prefix, StringComparison.OrdinalIgnoreCase));
        ids = ids.Order(PartitionIdCollection.Comparer);
        if (after != null) ids = ids.Where(id => PartitionIdCollection.Comparer.Compare(id, after) > 0);
        var items = ids.Take(maxResults + 1).ToList();
        var hasMore = items.Count > maxResults;
        if (hasMore) items.RemoveAt(maxResults);
        return this.Ok(new PartitionIdCollection()
        {
            Items = items,
            ContinuationToken = hasMore ? Base64Url.EncodeToString(Encoding.UTF8.GetBytes(items[^1])) : null
        });
    }

    static string DecodeContinuationToken(string continuationToken)
    {
        try { return Encoding.UTF8.GetString(Base64Url.DecodeFromChars(continuationToken)); }
        catch (FormatException) { throw BadRequest("The specified continuation token is invalid"); }
    }

    static ProblemDetailsException BadRequest(string detail) => new(new(new Uri("https://cloud-streams.io/problems/validation-failed"), "Validation Failed", (int)HttpStatusCode.BadRequest, detail));

}
