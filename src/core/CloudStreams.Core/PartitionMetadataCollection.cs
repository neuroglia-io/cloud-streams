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

namespace CloudStreams.Core;

/// <summary>
/// Represents a page of partitions, with their metadata, sorted like a <see cref="PartitionIdCollection"/> (see #226)
/// </summary>
[DataContract]
public record PartitionMetadataCollection
{

    /// <summary>
    /// Gets the maximum amount of partitions a page may hold, which is lower than for ids alone, since the metadata of each partition is read
    /// </summary>
    public const int MaxResultsLimit = 100;

    /// <summary>
    /// Gets the default amount of partitions a page holds
    /// </summary>
    public const int DefaultMaxResults = 25;

    /// <summary>
    /// Gets/sets the metadata of the partitions of the page
    /// </summary>
    [DataMember(Order = 1, Name = "items"), JsonPropertyName("items")]
    public virtual List<PartitionMetadata> Items { get; set; } = [];

    /// <summary>
    /// Gets/sets the token used to get the next page, if there is one
    /// </summary>
    [DataMember(Order = 2, Name = "continuationToken"), JsonPropertyName("continuationToken")]
    public virtual string? ContinuationToken { get; set; }

}
