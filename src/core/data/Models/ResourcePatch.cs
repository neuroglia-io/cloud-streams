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
/// Describes a patch that applies to a resource
/// </summary>
[DataContract]
public class ResourcePatch
{

    /// <summary>
    /// Initializes a new <see cref="ResourcePatch"/>
    /// </summary>
    public ResourcePatch() { }

    /// <summary>
    /// Initializes a new <see cref="ResourcePatch"/>
    /// </summary>
    /// <param name="resource">A reference to the resource to patch</param>
    /// <param name="patch">The patch to apply</param>
    public ResourcePatch(ResourceReference resource, Patch patch)
    {
        if (resource == null) throw new ArgumentNullException(nameof(resource));
        if (patch == null) throw new ArgumentNullException(nameof(patch));
        this.Resource = resource;
        this.Patch = patch;
    }

    /// <summary>
    /// Gets/sets a reference to the resource to patch
    /// </summary>
    [Required, JsonRequired]
    [DataMember(Order = 1, Name = "resource", IsRequired = true), JsonPropertyName("resource"), YamlMember(Alias = "resource")]
    public virtual ResourceReference Resource { get; set; } = null!;

    /// <summary>
    /// Gets/sets the patch to apply
    /// </summary>
    [Required, JsonRequired]
    [DataMember(Order = 2, Name = "patch", IsRequired = true), JsonPropertyName("patch"), YamlMember(Alias = "patch")]
    public virtual Patch Patch { get; set; } = null!;

}

/// <summary>
/// Describes a patch that applies to a resource
/// </summary>
[DataContract]
public class ResourcePatch<TResource>
    where TResource : class, IResource, new()
{

    /// <summary>
    /// Initializes a new <see cref="ResourcePatch"/>
    /// </summary>
    public ResourcePatch() { }

    /// <summary>
    /// Initializes a new <see cref="ResourcePatch"/>
    /// </summary>
    /// <param name="resource">A reference to the resource to patch</param>
    /// <param name="patch">The patch to apply</param>
    public ResourcePatch(ResourceReference<TResource> resource, Patch patch)
    {
        if (resource == null) throw new ArgumentNullException(nameof(resource));
        if (patch == null) throw new ArgumentNullException(nameof(patch));
        this.Resource = resource;
        this.Patch = patch;
    }

    /// <summary>
    /// Gets/sets a reference to the resource to patch
    /// </summary>
    [Required, JsonRequired]
    [DataMember(Order = 1, Name = "resource", IsRequired = true), JsonPropertyName("resource"), YamlMember(Alias = "resource")]
    public virtual ResourceReference<TResource> Resource { get; set; } = null!;

    /// <summary>
    /// Gets/sets the patch to apply
    /// </summary>
    [Required, JsonRequired]
    [DataMember(Order = 2, Name = "patch", IsRequired = true), JsonPropertyName("patch"), YamlMember(Alias = "patch")]
    public virtual Patch Patch { get; set; } = null!;

}