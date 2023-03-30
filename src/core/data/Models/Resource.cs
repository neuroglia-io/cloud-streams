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
/// Represents the base class of all cloud stream resources
/// </summary>
public class Resource
    : IResource
{

    /// <summary>
    /// Initializes a new <see cref="Resource"/>
    /// </summary>
    public Resource() { }

    /// <summary>
    /// Initializes a new <see cref="Resource"/>
    /// </summary>
    /// <param name="type">An object used to describe the <see cref="Resource"/>'s type</param>
    public Resource(ResourceType type) 
    {
        this.Type = type ?? throw new ArgumentNullException(nameof(type));
        this.ApiVersion = this.Type.GetApiVersion();
        this.Kind = this.Type.Kind;
    }

    /// <summary>
    /// Initializes a new <see cref="Resource"/>
    /// </summary>
    /// <param name="type">An object used to describe the <see cref="Resource"/>'s type</param>
    /// <param name="metadata">The object that describes the resource</param>
    public Resource(ResourceType type, ResourceMetadata metadata)
        : this(type)
    {
        this.Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
    }

    /// <summary>
    /// Gets the resource's API version
    /// </summary>
    [Required, JsonRequired, JsonPropertyOrder(-1000)]
    [DataMember(Order = 1, Name = "apiVersion", IsRequired = true), JsonPropertyName("apiVersion"), YamlMember(Alias = "apiVersion")]
    public virtual string ApiVersion { get; set; } = null!;

    /// <summary>
    /// Gets the resource's kind
    /// </summary>
    [Required, JsonRequired, JsonPropertyOrder(-999)]
    [DataMember(Order = 2, Name = "kind"), JsonPropertyName("kind"), YamlMember(Alias = "kind")]
    public virtual string Kind { get; set; } = null!;

    /// <summary>
    /// Gets/sets the object that describes the resource
    /// </summary>
    [Required, JsonRequired, JsonPropertyOrder(-998)]
    [DataMember(Order = 3, Name = "metadata", IsRequired = true), JsonPropertyName("metadata"), YamlMember(Alias = "metadata")]
    public virtual ResourceMetadata Metadata { get; set; } = null!;

    object IMetadata.Metadata => this.Metadata;

    /// <inheritdoc/>
    [IgnoreDataMember, JsonIgnore, YamlIgnore]
    public virtual ResourceType Type { get; } = null!;

    /// <inheritdoc/>
    [DataMember(Order = 999, Name = "extensionData"), JsonExtensionData]
    public IDictionary<string, object>? ExtensionData { get; set; }

}

/// <summary>
/// Represents the base class of all cloud stream resources
/// </summary>
/// <typeparam name="TSpec">The type of the resource's spec</typeparam>
public class Resource<TSpec>
    : Resource, IResource<TSpec>
    where TSpec : class, new()
{

    /// <summary>
    /// Initializes a new <see cref="Resource"/>
    /// </summary>
    public Resource() { }

    /// <summary>
    /// Initializes a new <see cref="Resource"/>
    /// </summary>
    /// <param name="type">An object used to describe the <see cref="Resource"/>'s type</param>
    public Resource(ResourceType type) : base(type) { }

    /// <summary>
    /// Initializes a new <see cref="Resource"/>
    /// </summary>
    /// <param name="type">An object used to describe the resource's type</param>
    /// <param name="metadata">The object that describes the resource</param>
    /// <param name="spec">The resource's spec</param>
    public Resource(ResourceType type, ResourceMetadata metadata, TSpec spec)
        : base(type, metadata)
    {
        this.Spec = spec ?? throw new ArgumentNullException(nameof(spec));
    }

    /// <summary>
    /// Gets/sets the object used to define and configure the resource
    /// </summary>
    [JsonPropertyOrder(-997)]
    [DataMember(Order = 1, Name = "spec"), JsonPropertyName("spec"), YamlMember(Alias = "spec")]
    public virtual TSpec Spec { get; set; } = null!;

    object ISpec.Spec => this.Spec;

}

/// <summary>
/// Represents the base class of all cloud stream resources
/// </summary>
/// <typeparam name="TSpec">The type of the resource's spec</typeparam>
/// <typeparam name="TStatus">The type of the resource's status</typeparam>
public class Resource<TSpec, TStatus>
    : Resource<TSpec>, IResource<TSpec, TStatus>
    where TSpec : class, new()
    where TStatus : class, new()
{

    /// <summary>
    /// Initializes a new <see cref="Resource"/>
    /// </summary>
    public Resource() { }

    /// <summary>
    /// Initializes a new <see cref="Resource"/>
    /// </summary>
    /// <param name="type">An object used to describe the <see cref="Resource"/>'s type</param>
    public Resource(ResourceType type) : base(type) { }

    /// <summary>
    /// Initializes a new <see cref="Resource"/>
    /// </summary>
    /// <param name="type">An object used to describe the resource's type</param>
    /// <param name="metadata">The object that describes the resource</param>
    /// <param name="spec">The resource's spec</param>
    /// <param name="status">An object that describes the resource's status</param>
    public Resource(ResourceType type, ResourceMetadata metadata, TSpec spec, TStatus? status = null)
        : base(type, metadata, spec)
    {
        this.Status = status;
    }

    /// <summary>
    /// Gets/sets an object that describes the resource's status, if any
    /// </summary>
    [JsonPropertyOrder(-996)]
    [DataMember(Order = 1, Name = "status"), JsonPropertyName("status"), YamlMember(Alias = "status")]
    public virtual TStatus? Status { get; set; }

    object? IStatus.Status => this.Status;

}