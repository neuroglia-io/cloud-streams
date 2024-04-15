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
/// Represents an object used to describe a cloud event stream
/// </summary>
[DataContract]
public record StreamMetadata
{

    /// <summary>
    /// Gets/sets the date and time at which the first event has been appended to the described stream
    /// </summary>
    [DataMember(Order = 1, Name = "firstEvent", IsRequired = true), JsonPropertyName("firstEvent"), YamlMember(Alias = "firstEvent")]
    public virtual DateTimeOffset? FirstEvent { get; set; }

    /// <summary>
    /// Gets/sets the date and time at which the last event has been appended to the described stream
    /// </summary>
    [DataMember(Order = 2, Name = "lastEvent", IsRequired = true), JsonPropertyName("lastEvent"), YamlMember(Alias = "lastEvent")]
    public virtual DateTimeOffset? LastEvent { get; set; }

    /// <summary>
    /// Gets/sets the described stream's length
    /// </summary>
    [DataMember(Order = 3, Name = "length", IsRequired = true), JsonPropertyName("length"), YamlMember(Alias = "length")]
    public virtual ulong? Length { get; set; }

}
