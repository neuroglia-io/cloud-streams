namespace CloudStreams.Core.Data.Models;

/// <summary>
/// Represents an object used to configure schema-based validation of incoming cloud events' data
/// </summary>
[DataContract]
public class DataSchemaValidationPolicy
{

    /// <summary>
    /// Gets/sets a boolean indicating whether or not inbound cloud events should define a valid data schema
    /// </summary>
    [DefaultValue(true)]
    [DataMember(Order = 1, Name = "required"), JsonPropertyName("required"), YamlMember(Alias = "required")]
    public virtual bool Required { get; set; } = true;

    /// <summary>
    /// Gets/sets a boolean indicating whether or not schemas for unknown inbound cloud events for be automatically generated and registered in the application's schema registry
    /// </summary>
    [DataMember(Order = 2, Name = "autoGenerate"), JsonPropertyName("autoGenerate"), YamlMember(Alias = "autoGenerate")]
    public virtual bool AutoGenerate { get; set; }

}
