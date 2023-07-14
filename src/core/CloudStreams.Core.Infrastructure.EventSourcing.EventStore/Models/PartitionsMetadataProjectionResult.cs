// Copyright © 2023-Present The Cloud Streams Authors
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

using CloudStreams.Core.Data;
using System.Text.Json.Serialization;

namespace CloudStreams.Core.Infrastructure.Models;

/// <summary>
/// Represents the results of the partition of a <see cref="CloudEventPartitionType"/> metadata projection
/// </summary>
internal class PartitionsMetadataProjectionResult
{

    /// <summary>
    /// Gets/Sets existing ids of a <see cref="CloudEventPartitionType"/>
    /// </summary>
    [JsonPropertyName("keys")]
    public List<string> Keys { get; set; } = null!;

    /// <summary>
    /// Gets/Sets the metadata entries of a <see cref="CloudEventPartitionType"/>
    /// </summary>
    [JsonPropertyName("values")]
    public Dictionary<string, PartitionMetadata> Values { get; set; } = null!;

}
