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

using Neuroglia.Eventing.CloudEvents;

namespace CloudStreams.Core.Api.Client.Services;

/// <summary>
/// Defines the fundamentals of the Cloud Streams gateway API used to manage <see cref="CloudEvent"/> partitions
/// </summary>
public interface ICloudEventPartitionsApi
{
    
    /// <summary>
    /// Gets the metadata used to describe the specified <see cref="CloudEvent"/> partition
    /// </summary>
    /// <param name="type">The type of the <see cref="CloudEvent"/> partition to get the metadata of</param>
    /// <param name="id">The id of the <see cref="CloudEvent"/> partition to get the metadata of</param>
    /// <param name="namespace">The namespace whose cloud events to read, if any. The partitions of the default namespace, and the whole stream, are read if not set</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="PartitionMetadata"/> used to describe the specified <see cref="CloudEvent"/> partition</returns>
    Task<PartitionMetadata?> GetPartitionMetadataAsync(CloudEventPartitionType type, string id, string? @namespace = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists, page by page, the ids of the partitions of the specified type, sorted alphabetically
    /// </summary>
    /// <param name="type">The type of the partitions to list the ids of</param>
    /// <param name="prefix">The prefix, matched case-insensitively, the ids to list must start with, if any</param>
    /// <param name="maxResults">The maximum amount of ids to return, between 1 and 1000. Defaults to 100</param>
    /// <param name="continuationToken">The token returned with the previous page, used to get the next one, if any</param>
    /// <param name="namespace">The namespace whose cloud events to read, if any. The partitions of the default namespace, and the whole stream, are read if not set</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="PartitionIdCollection"/> containing the requested page of partition ids</returns>
    Task<PartitionIdCollection> ListPartitionsByTypeAsync(CloudEventPartitionType type, string? prefix = null, int? maxResults = null, string? continuationToken = null, string? @namespace = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists, page by page, the partitions of the specified type with their metadata, sorted like their ids
    /// </summary>
    /// <param name="type">The type of the partitions to list</param>
    /// <param name="prefix">The prefix, matched case-insensitively, the ids of the partitions to list must start with, if any</param>
    /// <param name="maxResults">The maximum amount of partitions to return, between 1 and 100. Defaults to 25</param>
    /// <param name="continuationToken">The token returned with the previous page, used to get the next one, if any</param>
    /// <param name="namespace">The namespace whose cloud events to read, if any. The partitions of the default namespace, and the whole stream, are read if not set</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="PartitionMetadataCollection"/> containing the requested page of partitions</returns>
    Task<PartitionMetadataCollection> ListPartitionMetadataByTypeAsync(CloudEventPartitionType type, string? prefix = null, int? maxResults = null, string? continuationToken = null, string? @namespace = null, CancellationToken cancellationToken = default);

}