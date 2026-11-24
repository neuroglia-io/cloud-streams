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
/// Represents a page of cloud event partition ids, sorted alphabetically
/// </summary>
[DataContract]
public record PartitionIdCollection
{

    /// <summary>
    /// Gets the default maximum amount of partition ids returned in a page
    /// </summary>
    public const int DefaultMaxResults = 100;

    /// <summary>
    /// Gets the maximum amount of partition ids that can be requested in a page
    /// </summary>
    public const int MaxResultsLimit = 1000;

    /// <summary>
    /// Gets the comparer used to sort partition ids: case-insensitive and culture-independent, with ids that only differ by case ordered deterministically
    /// </summary>
    public static readonly IComparer<string> Comparer = Comparer<string>.Create((x, y) =>
    {
        var result = StringComparer.OrdinalIgnoreCase.Compare(x, y);
        return result != 0 ? result : StringComparer.Ordinal.Compare(x, y);
    });

    /// <summary>
    /// Gets/sets the partition ids of the page, sorted alphabetically
    /// </summary>
    [DataMember(Order = 1, Name = "items"), JsonPropertyOrder(1), JsonPropertyName("items"), YamlMember(Order = 1, Alias = "items")]
    public virtual List<string> Items { get; set; } = [];

    /// <summary>
    /// Gets/sets the opaque token used to get the next page, if any. Null on the last page
    /// </summary>
    [DataMember(Order = 2, Name = "continuationToken"), JsonPropertyOrder(2), JsonPropertyName("continuationToken"), YamlMember(Order = 2, Alias = "continuationToken")]
    public virtual string? ContinuationToken { get; set; }

}
