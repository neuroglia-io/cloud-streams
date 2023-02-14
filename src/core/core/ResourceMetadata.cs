using YamlDotNet.Serialization;

namespace CloudStreams;

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

}
