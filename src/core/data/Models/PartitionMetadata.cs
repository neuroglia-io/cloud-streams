namespace CloudStreams.Core.Data.Models;

/// <summary>
/// Represents an object used to describe a cloud event partition
/// </summary>
[DataContract]
public class PartitionMetadata
{

    /// <summary>
    /// Gets/sets the described partition's type
    /// </summary>
    [Required, JsonRequired]
    [DataMember(Order = 1, Name = "type", IsRequired = true), JsonPropertyName("type"), YamlMember(Alias = "type")]
    public virtual CloudEventPartitionType Type { get; set; }

    /// <summary>
    /// Gets/sets the id of the described partition
    /// </summary>
    [Required, JsonRequired, MinLength(1)]
    [DataMember(Order = 2, Name = "id", IsRequired = true), JsonPropertyName("id"), YamlMember(Alias = "id")]
    public virtual string Id { get; set; } = null!;

    /// <summary>
    /// Gets/sets the date and time at which the first event has been partitioned
    /// </summary>
    [DataMember(Order = 3, Name = "firstEvent"), JsonPropertyName("firstEvent"), YamlMember(Alias = "firstEvent")]
    public virtual DateTimeOffset FirstEvent { get; set; }

    /// <summary>
    /// Gets/sets the date and time at which the last event has been partitioned
    /// </summary>
    [DataMember(Order = 4, Name = "lastEvent"), JsonPropertyName("lastEvent"), YamlMember(Alias = "lastEvent")] 
    public virtual DateTimeOffset LastEvent { get; set; }

    /// <summary>
    /// Gets/sets the length of the described partition
    /// </summary>
    [DataMember(Order = 5, Name = "length"), JsonPropertyName("length"), YamlMember(Alias = "length")]
    public virtual ulong Length { get; set; }

}