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
/// Describes a patch
/// </summary>
[DataContract]
public class Patch
{

    /// <summary>
    /// Initializes a new <see cref="Patch"/>
    /// </summary>
    public Patch() { }

    /// <summary>
    /// Initializes a new <see cref="Patch"/>
    /// </summary>
    /// <param name="type">The type of patch to apply</param>
    /// <param name="document">The patch document</param>
    public Patch(PatchType type, object document)
    {
        this.Type = type;
        this.Document = document;
    }

    /// <summary>
    /// Gets/sets the patch's type
    /// </summary>
    [DataMember(Order = 1, Name = "type"), JsonPropertyName("type"), YamlMember(Alias = "type")]
    public virtual PatchType Type { get; set; }

    /// <summary>
    /// Gets/sets the patch document
    /// </summary>
    [Required, JsonRequired]
    [DataMember(Order = 2, Name = "document", IsRequired = true), JsonPropertyName("document"), YamlMember(Alias = "document")]
    public virtual object Document { get; set; } = null!;

}