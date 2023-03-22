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
/// Represents an event produced during a watch
/// </summary>
[DataContract]
public class ResourceWatchEvent
    : IResourceWatchEvent
{

    /// <summary>
    /// Initializes a new <see cref="ResourceWatchEvent"/>
    /// </summary>
    public ResourceWatchEvent() { }

    /// <summary>
    /// Initializes a new <see cref="ResourceWatchEvent"/>
    /// </summary>
    /// <param name="type">The <see cref="ResourceWatchEvent"/>'s type</param>
    /// <param name="resource">The resource that has produced the <see cref="ResourceWatchEvent"/></param>
    /// <exception cref="ArgumentNullException"></exception>
    public ResourceWatchEvent(ResourceWatchEventType type, Resource resource)
    {
        if (resource == null) throw new ArgumentNullException(nameof(resource));
        this.Type = type;
        this.Resource = resource;
    }

    /// <summary>
    /// Gets/sets the event's type<para></para>
    /// See <see cref="ResourceWatchEventType"/>
    /// </summary>
    [DataMember(Order = 1, Name = "type"), JsonPropertyName("type"), YamlMember(Alias = "type")]
    public virtual ResourceWatchEventType Type { get; set; }

    /// <summary>
    /// Gets/sets the resource that has produced the event
    /// </summary>
    [DataMember(Order = 2, Name = "resource"), JsonPropertyName("resource"), YamlMember(Alias = "resource")]
    public virtual Resource Resource { get; set; } = null!;

    IResource IResourceWatchEvent.Resource => this.Resource;

}

/// <summary>
/// Represents an event produced during a watch
/// </summary>
[DataContract]
public class ResourceWatchEvent<TResource>
    : IResourceWatchEvent<TResource>
    where TResource : class, IResource, new()
{

    /// <summary>
    /// Initializes a new <see cref="ResourceWatchEvent{TResource}"/>
    /// </summary>
    public ResourceWatchEvent() { }

    /// <summary>
    /// Initializes a new <see cref="ResourceWatchEvent{TResource}"/>
    /// </summary>
    /// <param name="type">The <see cref="ResourceWatchEvent{TResource}"/>'s type</param>
    /// <param name="resource">The resource that has produced the <see cref="ResourceWatchEvent{TResource}"/></param>
    public ResourceWatchEvent(ResourceWatchEventType type, TResource resource)
    {
        if (resource == null) throw new ArgumentNullException(nameof(resource));
        this.Type = type;
        this.Resource = resource;
    }

    /// <summary>
    /// Gets/sets the event's type<para></para>
    /// See <see cref="ResourceWatchEventType"/>
    /// </summary>
    [DataMember(Order = 1, Name = "type"), JsonPropertyName("type"), YamlMember(Alias = "type")]
    public virtual ResourceWatchEventType Type { get; set; }

    /// <summary>
    /// Gets/sets the resource that has produced the <see cref="ResourceWatchEvent"/>
    /// </summary>
    [DataMember(Order = 2, Name = "resource"), JsonPropertyName("resource"), YamlMember(Alias = "resource")]
    public virtual TResource Resource { get; set; } = null!;

    IResource IResourceWatchEvent.Resource => this.Resource;

}
