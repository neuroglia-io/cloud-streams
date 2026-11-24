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

namespace CloudStreams.Core.Application.Services;

/// <summary>
/// Defines the fundamentals of the index of the partitions cloud events have been published to
/// </summary>
/// <remarks>
/// The index keeps the ids of each type of partition sorted, as defined by <see cref="PartitionIdCollection.Comparer"/>, so that they can be listed page by page and
/// searched by prefix without loading them all (see #185)
/// </remarks>
public interface IPartitionIndex
{

    /// <summary>
    /// Adds the specified partitions to the index
    /// </summary>
    /// <param name="partitions">The partitions to add</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    Task AddAsync(IEnumerable<PartitionReference> partitions, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the ids of the partitions of the specified type
    /// </summary>
    /// <param name="partitionType">The type of the partitions to list</param>
    /// <param name="prefix">The prefix, compared case-insensitively, of the ids to list, if any</param>
    /// <param name="after">The id after which to start listing, if any</param>
    /// <param name="maxResults">The maximum number of ids to list</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The sorted ids of the partitions of the specified type</returns>
    Task<IReadOnlyList<string>> ListAsync(CloudEventPartitionType partitionType, string? prefix, string? after, int maxResults, CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines whether the index of the specified type of partitions has been initialized, which is to say backfilled from the projections of earlier versions
    /// </summary>
    /// <param name="partitionType">The type of partitions to check</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A boolean indicating whether or not the index has been initialized</returns>
    Task<bool> IsInitializedAsync(CloudEventPartitionType partitionType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks the index of the specified type of partitions as initialized
    /// </summary>
    /// <param name="partitionType">The type of partitions to mark as initialized</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    Task MarkInitializedAsync(CloudEventPartitionType partitionType, CancellationToken cancellationToken = default);

}
