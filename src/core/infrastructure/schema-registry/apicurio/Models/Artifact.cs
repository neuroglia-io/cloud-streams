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

using CloudStreams.Core.Serialization.Json.Converters;
using System.Text.Json.Serialization;

namespace CloudStreams.Core.Infrastructure.SchemaRegistry.Apicurio.Models;

/// <summary>
/// Describes an artifact
/// </summary>
public class Artifact
{

    /// <summary>
    /// Gets/sets the <see cref="Artifact"/>'s id
    /// </summary>
    [JsonPropertyName("id")]
    public virtual string Id { get; set; } = null!;

    /// <summary>
    /// Gets/sets the <see cref="Artifact"/>'s global id
    /// </summary>
    [JsonPropertyName("globalId")]
    public virtual long? GlobalId { get; set; } = null!;

    /// <summary>
    /// Gets/sets the <see cref="Artifact"/>'s contentId
    /// </summary>
    [JsonPropertyName("contentId")]
    public virtual long? ContentId { get; set; } = null!;

    /// <summary>
    /// Gets/sets the <see cref="Artifact"/>'s name
    /// </summary>
    [JsonPropertyName("name")]
    public virtual string Name { get; set; } = null!;

    /// <summary>
    /// Gets/sets the <see cref="Artifact"/>'s description
    /// </summary>
    [JsonPropertyName("description")]
    public virtual string Description { get; set; } = null!;

    /// <summary>
    /// Gets/sets date and time at which the <see cref="Artifact"/> has been created
    /// </summary>
    [JsonPropertyName("createdOn"), JsonConverter(typeof(DateTimeParserConverter))]
    public virtual DateTime CreatedOn { get; set; }

    /// <summary>
    /// Gets/sets the user the <see cref="Artifact"/> has been created by
    /// </summary>
    [JsonPropertyName("createdBy")]
    public virtual string CreatedBy { get; set; } = null!;

    /// <summary>
    /// Gets/sets <see cref="Artifact"/>'s type
    /// </summary>
    [JsonPropertyName("type")]
    public virtual ArtifactType Type { get; set; }

    /// <summary>
    /// Gets/sets a collection containing the <see cref="Artifact"/>'s labels
    /// </summary>
    [JsonPropertyName("labels")]
    public virtual ICollection<string>? Labels { get; set; }

    /// <summary>
    /// Gets/sets the <see cref="Artifact"/>'s state
    /// </summary>
    [JsonPropertyName("state")]
    public virtual ArtifactState State { get; set; }

    /// <summary>
    /// Gets the date and time the <see cref="Artifact"/> has last been modified on
    /// </summary>
    [JsonPropertyName("modifiedOn"), JsonConverter(typeof(DateTimeParserConverter))]
    public virtual DateTime? ModifiedOn { get; set; }

    /// <summary>
    /// Gets/sets the user the <see cref="Artifact"/> has last been modified by
    /// </summary>
    [JsonPropertyName("modifiedBy")]
    public virtual string? ModifiedBy { get; set; }

    /// <summary>
    /// Gets/sets the id of the <see cref="Artifact"/>'s group
    /// </summary>
    [JsonPropertyName("groupId")]
    public virtual string? GroupId { get; set; }

    /// <summary>
    /// Gets/sets the <see cref="Artifact"/>'s version
    /// </summary>
    [JsonPropertyName("version")]
    public virtual string? Version { get; set; }

    /// <summary>
    /// Gets/sets the <see cref="Artifact"/>'s user-defined name/value pairs
    /// </summary>
    [JsonPropertyName("properties")]
    public virtual IDictionary<string, string>? Properties { get; set; }

}
