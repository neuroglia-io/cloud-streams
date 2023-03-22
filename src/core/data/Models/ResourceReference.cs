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

using System.Diagnostics;

namespace CloudStreams.Core.Data.Models;

/// <summary>
/// Represents a reference to a resource
/// </summary>
[DataContract]
public class ResourceReference
    : IResourceReference
{

    /// <summary>
    /// Initializes a new <see cref="ResourceReference"/>
    /// </summary>
    public ResourceReference() { }

    /// <summary>
    /// Initializes a new <see cref="ResourceReference"/>
    /// </summary>
    /// <param name="apiVersion">The referenced resource's API version</param>
    /// <param name="kind">The referenced resource's kind</param>
    /// <param name="name">The name of the referenced resource</param>
    /// <param name="namespace">The namespace the referenced resource belongs to, if any</param>
    public ResourceReference(string apiVersion, string kind, string name, string? @namespace)
    {
        if (string.IsNullOrWhiteSpace(apiVersion)) throw new ArgumentNullException(nameof(apiVersion));
        if (string.IsNullOrWhiteSpace(kind)) throw new ArgumentNullException(nameof(kind));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));
        this.Name = name;
        this.Namespace = @namespace;
    }

    /// <inheritdoc/>
    [Required, JsonRequired]
    [DataMember(Order = 1, Name = "apiVersion", IsRequired = true), JsonPropertyName("apiVersion"), YamlMember(Alias = "apiVersion")]
    public virtual string ApiVersion { get; set; } = null!;

    /// <inheritdoc/>
    [Required, JsonRequired]
    [DataMember(Order = 2, Name = "kind", IsRequired = true), JsonPropertyName("kind"), YamlMember(Alias = "kind")]
    public virtual string Kind { get; set; } = null!;

    /// <inheritdoc/>
    [Required, JsonRequired]
    [DataMember(Order = 3, Name = "name", IsRequired = true), JsonPropertyName("name"), YamlMember(Alias = "name")]
    public virtual string Name { get; set; } = null!;

    /// <inheritdoc/>
    [DataMember(Order = 4), JsonPropertyName("namespace"), YamlMember(Alias = "namespace")]
    public virtual string? Namespace { get; set; }

    /// <summary>
    /// Creates a new <see cref="ResourceReference"/> for the specified <see cref="IResource"/>
    /// </summary>
    /// <param name="resource">The <see cref="IResource"/> to get a reference to</param>
    public static implicit operator ResourceReference?(Resource? resource)
    {
        if (resource == null) return null;
        return new(resource.ApiVersion, resource.Kind, resource.GetName(), resource.GetNamespace());
    }

}

/// <summary>
/// Represents a reference to a resource
/// </summary>
/// <typeparam name="TResource">The type of the reference <see cref="IResource"/></typeparam>
[DataContract]
public class ResourceReference<TResource>
    : IResourceReference
    where TResource : class, IResource, new()
{

    private static readonly TResource Resource = new();

    string IResourceReference.ApiVersion => Resource.ApiVersion;

    string IResourceReference.Kind => Resource.Kind;

    /// <summary>
    /// Initializes a new <see cref="ResourceReference{TResource}"/>
    /// </summary>
    public ResourceReference() { }

    /// <summary>
    /// Initializes a new <see cref="ResourceReference{TResource}"/>
    /// </summary>
    /// <param name="name">The name of the referenced resource</param>
    /// <param name="namespace">The namespace the referenced resource belongs to, if any</param>
    public ResourceReference(string name, string? @namespace)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));
        this.Name = name;
        this.Namespace = @namespace;
    }

    /// <inheritdoc/>
    [Required, JsonRequired]
    [DataMember(Order = 1, Name = "name", IsRequired = true), JsonPropertyName("name"), YamlMember(Alias = "name")]
    public virtual string Name { get; set; } = null!;

    /// <inheritdoc/>
    [DataMember(Order = 2), JsonPropertyName("namespace"), YamlMember(Alias = "namespace")]
    public virtual string? Namespace { get; set; }

    /// <summary>
    /// Creates a new <see cref="ResourceReference"/> for the specified <see cref="IResource"/>
    /// </summary>
    /// <param name="resource">The <see cref="IResource"/> to get a reference to</param>
    public static implicit operator ResourceReference<TResource>?(TResource? resource)
    {
        if (resource == null) return null;
        return new(resource.GetName(), resource.GetNamespace());
    }

}