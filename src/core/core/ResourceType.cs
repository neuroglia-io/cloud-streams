using YamlDotNet.Serialization;

namespace CloudStreams.Core;

/// <summary>
/// Describes the type of a resource
/// </summary>
[DataContract]
public class ResourceType
{

    /// <summary>
    /// Initializes a new <see cref="ResourceType"/>
    /// </summary>
    public ResourceType() { }

    /// <summary>
    /// Initializes a new <see cref="ResourceType"/>
    /// </summary>
    /// <param name="group">The API group the resource type belongs to</param>
    /// <param name="version">The resource type's version</param>
    /// <param name="plural">The resource type's plural name</param>
    public ResourceType(string group, string version, string plural)
    {
        if (string.IsNullOrWhiteSpace(group)) throw new ArgumentNullException(nameof(group));
        if (string.IsNullOrWhiteSpace(version)) throw new ArgumentNullException(nameof(version));
        if (string.IsNullOrWhiteSpace(plural)) throw new ArgumentNullException(nameof(plural));
        this.Group = group;
        this.Version = version;
        this.Plural = plural;
    }

    /// <summary>
    /// Gets/sets the API group the resource type belongs to
    /// </summary>
    [DataMember(Order = 1, Name = "group"), JsonPropertyName("group"), YamlMember(Alias = "group")]
    public virtual string Group { get; set; } = null!;

    /// <summary>
    /// Gets/sets resource type's version
    /// </summary>
    [DataMember(Order = 2, Name = "group"), JsonPropertyName("group"), YamlMember(Alias = "group")]
    public virtual string Version { get; set; } = null!;

    /// <summary>
    /// Gets/sets the resource type's plural name
    /// </summary>
    [DataMember(Order = 3, Name = "group"), JsonPropertyName("group"), YamlMember(Alias = "group")]
    public virtual string Plural { get; set; } = null!;

}