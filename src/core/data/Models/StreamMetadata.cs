namespace CloudStreams.Core.Data.Models;

/// <summary>
/// Represents an object used to describe a cloud event stream
/// </summary>
[DataContract]
public class StreamMetadata
{

    /// <summary>
    /// Gets/sets the date and time at which the first event has been appended to the described stream
    /// </summary>
    [DataMember(Order = 1, Name = "firstEvent", IsRequired = true), JsonPropertyName("firstEvent"), YamlMember(Alias = "firstEvent")]
    public virtual DateTimeOffset? FirstEvent { get; set; }

    /// <summary>
    /// Gets/sets the date and time at which the last event has been appended to the described stream
    /// </summary>
    [DataMember(Order = 2, Name = "lastEvent", IsRequired = true), JsonPropertyName("lastEvent"), YamlMember(Alias = "lastEvent")]
    public virtual DateTimeOffset? LastEvent { get; set; }

    /// <summary>
    /// Gets/sets the described stream's length
    /// </summary>
    [DataMember(Order = 3, Name = "length", IsRequired = true), JsonPropertyName("length"), YamlMember(Alias = "length")]
    public virtual ulong? Length { get; set; }

}
