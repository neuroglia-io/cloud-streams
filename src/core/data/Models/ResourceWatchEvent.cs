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
    public ResourceWatchEvent(ResourceWatchEventType type, IResource resource)
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
    public virtual IResource Resource { get; set; } = null!;

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
