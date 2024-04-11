namespace CloudStreams.Core.Resources;

/// <summary>
/// Represents an object used to configure the way the metadata of ingested cloud events should be resolved
/// </summary>
[DataContract]
public record CloudEventMetadataResolutionConfiguration
{

    /// <summary>
    /// Initializes a new <see cref="CloudEventMetadataResolutionConfiguration"/>
    /// </summary>
    public CloudEventMetadataResolutionConfiguration() { }

    /// <summary>
    /// Initializes a new <see cref="CloudEventMetadataResolutionConfiguration"/>
    /// </summary>
    /// <param name="properties">A list containing the configuration of the resolution of a cloud event's metadata properties</param>
    public CloudEventMetadataResolutionConfiguration(IEnumerable<CloudEventMetadataPropertyResolver> properties)
    {
        this.Properties = properties?.ToList();
    }

    /// <summary>
    /// Gets/sets a list containing the configuration of the resolution of a cloud event's metadata properties
    /// </summary>
    [DataMember(Order = 1, Name = "properties"), JsonPropertyName("properties"), YamlMember(Alias = "properties")]
    public virtual List<CloudEventMetadataPropertyResolver>? Properties { get; set; }

}
