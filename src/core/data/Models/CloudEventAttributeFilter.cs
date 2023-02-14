namespace CloudStreams.Data.Models;

/// <summary>
/// Represents an object used to configuring filtering of cloud events based on context attribute
/// </summary>
[DataContract]
public class CloudEventAttributeFilter
{

    /// <summary>
    /// Gets/sets the name of the attribute to filter
    /// </summary>
    [Required, JsonRequired, MinLength(1)]
    [DataMember(Order = 1, Name = "name", IsRequired = true), JsonPropertyName("name"), YamlMember(Alias = "name")]
    public virtual string Name { get; set; } = null!;

    /// <summary>
    /// Gets/sets the value of the attribute to filter
    /// </summary>
    [DataMember(Order = 2, Name = "value"), JsonPropertyName("value"), YamlMember(Alias = "value")]
    public virtual string? Value { get; set; }

}
