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

namespace CloudStreams.Core.Resources;

/// <summary>
/// Represents a registered JSON schema, whose versions are kept, and must be compatible with each other as configured (see #213)
/// </summary>
[DataContract]
public record Schema
    : Resource<SchemaSpec>
{

    /// <summary>
    /// Gets the <see cref="Schema"/>'s resource type
    /// </summary>
    public static readonly ResourceDefinitionInfo ResourceDefinition = new SchemaResourceDefinition()!;

    /// <summary>
    /// Initializes a new <see cref="Schema"/>
    /// </summary>
    public Schema() : base(ResourceDefinition) { }

    /// <summary>
    /// Initializes a new <see cref="Schema"/>
    /// </summary>
    /// <param name="metadata">The <see cref="Schema"/>'s metadata</param>
    /// <param name="spec">The <see cref="Schema"/>'s spec</param>
    public Schema(ResourceMetadata metadata, SchemaSpec spec) : base(ResourceDefinition, metadata, spec) { }

}

/// <summary>
/// Represents an object used to configure a <see cref="Schema"/>
/// </summary>
[DataContract]
public record SchemaSpec
{

    /// <summary>
    /// Gets/sets how each version of the schema must be compatible with the previous one. See <see cref="SchemaCompatibility"/>. Defaults to <see cref="SchemaCompatibility.Backward"/>
    /// </summary>
    [DataMember(Order = 1, Name = "compatibility"), JsonPropertyOrder(1), JsonPropertyName("compatibility"), YamlMember(Order = 1, Alias = "compatibility")]
    public virtual string? Compatibility { get; set; }

    /// <summary>
    /// Gets/sets the versions of the schema, from the oldest to the latest. Versions can be added, but neither changed nor removed
    /// </summary>
    [Required, JsonRequired]
    [DataMember(Order = 2, Name = "versions", IsRequired = true), JsonPropertyOrder(2), JsonPropertyName("versions"), YamlMember(Order = 2, Alias = "versions")]
    public virtual List<SchemaVersion> Versions { get; set; } = [];

    /// <summary>
    /// Gets the compatibility each version must have with the previous one
    /// </summary>
    /// <returns>The compatibility each version must have with the previous one</returns>
    public virtual string GetCompatibility() => string.IsNullOrWhiteSpace(this.Compatibility) ? SchemaCompatibility.Backward : this.Compatibility;

    /// <summary>
    /// Gets the specified version, or the latest one
    /// </summary>
    /// <param name="version">The version to get, if any. If not set, gets the latest one</param>
    /// <returns>The specified version, or null if it does not exist</returns>
    public virtual SchemaVersion? GetVersion(int? version = null) => version.HasValue ? this.Versions.FirstOrDefault(v => v.Version == version.Value) : this.Versions.LastOrDefault();

}

/// <summary>
/// Represents a version of a <see cref="Schema"/>
/// </summary>
[DataContract]
public record SchemaVersion
{

    /// <summary>
    /// Gets/sets the version's number, which is greater than the one of the previous version
    /// </summary>
    [Required, JsonRequired]
    [DataMember(Order = 1, Name = "version", IsRequired = true), JsonPropertyOrder(1), JsonPropertyName("version"), YamlMember(Order = 1, Alias = "version")]
    public virtual int Version { get; set; }

    /// <summary>
    /// Gets/sets the JSON schema of the version
    /// </summary>
    [Required, JsonRequired]
    [DataMember(Order = 2, Name = "schema", IsRequired = true), JsonPropertyOrder(2), JsonPropertyName("schema"), YamlMember(Order = 2, Alias = "schema")]
    public virtual object Schema { get; set; } = null!;

}

/// <summary>
/// Enumerates the compatibilities each version of a <see cref="Schema"/> may be required to have with the previous one
/// </summary>
public static class SchemaCompatibility
{

    /// <summary>
    /// Indicates that each version must accept the data the previous one accepts, so that consumers using the new version can read data produced with the previous one
    /// </summary>
    public const string Backward = "backward";
    /// <summary>
    /// Indicates that the previous version must accept the data each version accepts, so that consumers still using the previous version can read data produced with the new one
    /// </summary>
    public const string Forward = "forward";
    /// <summary>
    /// Indicates that each version must be both backward and forward compatible with the previous one
    /// </summary>
    public const string Full = "full";
    /// <summary>
    /// Indicates that versions are not checked for compatibility
    /// </summary>
    public const string None = "none";

    /// <summary>
    /// Gets an <see cref="IEnumerable{T}"/> containing all supported values
    /// </summary>
    /// <returns>A new <see cref="IEnumerable{T}"/> containing all supported values</returns>
    public static IEnumerable<string> AsEnumerable()
    {
        yield return Backward;
        yield return Forward;
        yield return Full;
        yield return None;
    }

}

/// <summary>
/// Represents a reference to a version of a <see cref="Schema"/>
/// </summary>
[DataContract]
public record SchemaReference
{

    /// <summary>
    /// Gets/sets the name of the referenced schema
    /// </summary>
    [Required, JsonRequired]
    [DataMember(Order = 1, Name = "name", IsRequired = true), JsonPropertyOrder(1), JsonPropertyName("name"), YamlMember(Order = 1, Alias = "name")]
    public virtual string Name { get; set; } = null!;

    /// <summary>
    /// Gets/sets the referenced version, if any. If not set, references the latest version
    /// </summary>
    [DataMember(Order = 2, Name = "version"), JsonPropertyOrder(2), JsonPropertyName("version"), YamlMember(Order = 2, Alias = "version")]
    public virtual int? Version { get; set; }

}
