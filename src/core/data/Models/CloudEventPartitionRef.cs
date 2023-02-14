using CloudStreams.Data.Attributes;

namespace CloudStreams.Data.Models;

/// <summary>
/// Represents an object used to reference a cloud event partition
/// </summary>
[DataContract]
public class CloudEventPartitionRef
{

    /// <summary>
    /// Initializes a new <see cref="CloudEventPartitionRef"/>
    /// </summary>
    public CloudEventPartitionRef() { }

    /// <summary>
    /// Initializes a new <see cref="CloudEventPartitionRef"/>
    /// </summary>
    /// <param name="type">The referenced stream partition's type</param>
    /// <param name="id">The referenced stream partition's id</param>
    public CloudEventPartitionRef(string type, string id)
    {
        if (string.IsNullOrWhiteSpace(type)) throw new ArgumentNullException(nameof(type));
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
        this.Type = type;
        this.Id = id;
    }

    /// <summary>
    /// Gets/sets the referenced stream partition's type
    /// </summary>
    [OneOf<string>(CloudEventPartitionType.BySource, CloudEventPartitionType.ByType, CloudEventPartitionType.BySubject)]
    [DataMember(Order = 1, Name = "type"), JsonPropertyName("type"), YamlMember(Alias = "type")]
    public virtual string Type { get; set; } = null!;

    /// <summary>
    /// Gets/sets the referenced stream partition's id
    /// </summary>
    [DataMember(Order = 2, Name = "id"), JsonPropertyName("id"), YamlMember(Alias = "id")]
    public virtual string Id { get; set; } = null!;

}
