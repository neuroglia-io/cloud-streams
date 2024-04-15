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
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="PartitionMetadata"/> used to describe the specified <see cref="CloudEvent"/> partition</returns>
    Task<PartitionMetadata?> GetPartitionMetadataAsync(CloudEventPartitionType type, string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the id of all partitions of the specified type
    /// </summary>
    /// <param name="type">The type of the partitions to list the ids of</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IAsyncEnumerable{T}"/> used to enumerate the id of all partitions of the specified type</returns>
    Task<IAsyncEnumerable<string?>> ListPartitionsByTypeAsync(CloudEventPartitionType type, CancellationToken cancellationToken = default);

}