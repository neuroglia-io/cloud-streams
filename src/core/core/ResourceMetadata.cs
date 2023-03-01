using YamlDotNet.Serialization;

namespace CloudStreams.Core;

/// <summary>
/// Represents an object used to describe a resource
/// </summary>
[DataContract]
public class ResourceMetadata
{

    /// <summary>
    /// Gets/sets the described resource's name
    /// </summary>
    [DataMember(Order = 1, Name = "name"), JsonPropertyName("name"), YamlMember(Alias = "name")]
    public virtual string? Name { get; set; }

    /// <summary>
    /// Gets/sets the described resource's name
    /// </summary>
    [DataMember(Order = 2, Name = "namespace"), JsonPropertyName("namespace"), YamlMember(Alias = "namespace")]
    public virtual string? Namespace { get; set; }

    /// <summary>
    /// Gets/sets the date and time at which the described resource has been created
    /// </summary>
    [DataMember(Order = 3, Name = "creationTimestamp"), JsonPropertyName("creationTimestamp"), YamlMember(Alias = "creationTimestamp")]
    public virtual DateTime? CreationTimestamp { get; set; }

    /// <summary>
    /// Gets/sets the resource's generation, which represents the resource's spec version
    /// </summary>
    [DataMember(Order = 4, Name = "generation"), JsonPropertyName("generation"), YamlMember(Alias = "generation")]
    public virtual ulong Generation { get; set; }

    /// <summary>
    /// Gets/sets the resource's version, which changes everytime the resource is written to, including potential subresources (status, ...)
    /// </summary>
    [DataMember(Order = 5, Name = "resourceVersion"), JsonPropertyName("resourceVersion"), YamlMember(Alias = "resourceVersion")]
    public virtual string? ResourceVersion { get; set; }

    /// <summary>
    /// Gets/sets a key/value mappings of the described resource's labels, if any
    /// </summary>
    [DataMember(Order = 6, Name = "labels"), JsonPropertyName("labels"), YamlMember(Alias = "labels")]
    public virtual IDictionary<string, string>? Labels { get; set; }

}
