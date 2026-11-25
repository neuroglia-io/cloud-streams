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
using Neuroglia.Data.Infrastructure.EventSourcing;

namespace CloudStreams.Core.Application.Queries.Partitions;

/// <summary>
/// Represents the query used to list, page by page, the cloud event partitions of the specified type with their metadata (see #226)
/// </summary>
/// <param name="partitionType">The type of the partitions to list</param>
/// <param name="prefix">The prefix, matched case-insensitively, the ids of the partitions to list must start with, if any</param>
/// <param name="maxResults">The maximum amount of partitions to list</param>
/// <param name="continuationToken">The token returned with the previous page, used to get the next one, if any</param>
public class ListEventPartitionsQuery(CloudEventPartitionType partitionType, string? prefix = null, int? maxResults = null, string? continuationToken = null)
    : Query<PartitionMetadataCollection>
{

    /// <summary>
    /// Gets the type of the partitions to list
    /// </summary>
    public CloudEventPartitionType PartitionType { get; } = partitionType;

    /// <summary>
    /// Gets the prefix the ids of the partitions to list must start with, if any
    /// </summary>
    public string? Prefix { get; } = prefix;

    /// <summary>
    /// Gets the maximum amount of partitions to list
    /// </summary>
    public int? MaxResults { get; } = maxResults;

    /// <summary>
    /// Gets the token used to get the next page, if any
    /// </summary>
    public string? ContinuationToken { get; } = continuationToken;

}

/// <summary>
/// Represents the service used to handle <see cref="ListEventPartitionsQuery"/> instances
/// </summary>
/// <param name="eventStore">The service used to read the metadata of partitions</param>
/// <param name="partitionIndex">The index of partition ids, if any</param>
/// <remarks>The page of ids is listed like <see cref="ListEventPartitionIdsQuery"/> does, then the metadata of its partitions is read, a few at a time</remarks>
public class ListEventPartitionsQueryHandler(ICloudEventStore eventStore, IPartitionIndex? partitionIndex = null)
    : IQueryHandler<ListEventPartitionsQuery, PartitionMetadataCollection>
{

    /// <summary>
    /// Gets the maximum amount of partitions whose metadata is read concurrently
    /// </summary>
    public const int MaxConcurrency = 8;

    /// <inheritdoc/>
    public virtual async Task<IOperationResult<PartitionMetadataCollection>> HandleAsync(ListEventPartitionsQuery query, CancellationToken cancellationToken)
    {
        var maxResults = query.MaxResults ?? PartitionMetadataCollection.DefaultMaxResults;
        if (maxResults < 1 || maxResults > PartitionMetadataCollection.MaxResultsLimit)
            return new OperationResult<PartitionMetadataCollection>((int)HttpStatusCode.BadRequest, errors: [new Error(new Uri("https://cloud-streams.io/problems/validation-failed"), "Validation Failed", (int)HttpStatusCode.BadRequest, $"The maximum amount of results must be between 1 and {PartitionMetadataCollection.MaxResultsLimit}")]);
        var ids = await new ListEventPartitionIdsQueryHandler(eventStore, partitionIndex).HandleAsync(new ListEventPartitionIdsQuery(query.PartitionType, query.Prefix, maxResults, query.ContinuationToken), cancellationToken).ConfigureAwait(false);
        if (!ids.IsSuccess() || ids.Data == null) return new OperationResult<PartitionMetadataCollection>(ids.Status, errors: ids.Errors?.ToArray() ?? []);
        var items = new PartitionMetadata[ids.Data.Items.Count];
        await Parallel.ForEachAsync(Enumerable.Range(0, items.Length), new ParallelOptions() { MaxDegreeOfParallelism = MaxConcurrency, CancellationToken = cancellationToken }, async (index, token) =>
        {
            var partition = new PartitionReference(query.PartitionType, ids.Data.Items[index]);
            try { items[index] = await eventStore.GetPartitionMetadataAsync(partition, token).ConfigureAwait(false); }
            catch (Exception ex) when (ex is StreamNotFoundException || (ex is ProblemDetailsException notFound && notFound.Problem.Status == (int)HttpStatusCode.NotFound))
            {
                // a partition listed by the index may not have been projected yet: it is listed, without metadata
                items[index] = new() { Type = query.PartitionType, Id = partition.Id };
            }
        }).ConfigureAwait(false);
        return this.Ok(new PartitionMetadataCollection() { Items = [.. items], ContinuationToken = ids.Data.ContinuationToken });
    }

}
