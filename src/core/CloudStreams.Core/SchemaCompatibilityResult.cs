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
/// Describes whether a JSON schema is compatible with the latest version of a registered schema (see #213)
/// </summary>
[DataContract]
public record SchemaCompatibilityResult
{

    /// <summary>
    /// Gets/sets a boolean indicating whether or not the JSON schema would be accepted as the next version of the schema
    /// </summary>
    [DataMember(Order = 1, Name = "compatible"), JsonPropertyOrder(1), JsonPropertyName("compatible"), JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public virtual bool Compatible { get; set; }

    /// <summary>
    /// Gets/sets the compatibility the schema requires. See <see cref="Resources.SchemaCompatibility"/>
    /// </summary>
    [DataMember(Order = 2, Name = "compatibility"), JsonPropertyOrder(2), JsonPropertyName("compatibility")]
    public virtual string Compatibility { get; set; } = null!;

    /// <summary>
    /// Gets/sets the version the JSON schema has been checked against, if any
    /// </summary>
    [DataMember(Order = 3, Name = "version"), JsonPropertyOrder(3), JsonPropertyName("version")]
    public virtual int? Version { get; set; }

    /// <summary>
    /// Gets/sets the reasons why the JSON schema is not compatible, if it is not
    /// </summary>
    [DataMember(Order = 4, Name = "errors"), JsonPropertyOrder(4), JsonPropertyName("errors")]
    public virtual List<string> Errors { get; set; } = [];

}
