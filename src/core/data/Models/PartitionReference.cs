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

namespace CloudStreams.Core.Data.Models;

/// <summary>
/// Represents an object used to reference a cloud event partition
/// </summary>
[DataContract]
public class PartitionReference
{

    /// <summary>
    /// Initializes a new <see cref="PartitionReference"/>
    /// </summary>
    public PartitionReference() { }

    /// <summary>
    /// Initializes a new <see cref="PartitionReference"/>
    /// </summary>
    /// <param name="type">The referenced stream partition's type</param>
    /// <param name="id">The referenced stream partition's id</param>
    public PartitionReference(CloudEventPartitionType type, string id)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
        this.Type = type;
        this.Id = id;
    }

    /// <summary>
    /// Gets/sets the referenced stream partition's type
    /// </summary>
    [DataMember(Order = 1, Name = "type"), JsonPropertyName("type"), YamlMember(Alias = "type")]
    public virtual CloudEventPartitionType Type { get; set; }

    /// <summary>
    /// Gets/sets the referenced stream partition's id
    /// </summary>
    [DataMember(Order = 2, Name = "id"), JsonPropertyName("id"), YamlMember(Alias = "id")]
    public virtual string Id { get; set; } = null!;

}
