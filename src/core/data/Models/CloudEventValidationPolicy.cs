namespace CloudStreams.Core.Data.Models;

/// <summary>
/// Represents an object used to configure cloud event validation
/// </summary>
[DataContract]
public class CloudEventValidationPolicy
{

    /// <summary>
    /// Gets/sets a boolean indicating whether or not to skip cloud event validation
    /// </summary>
    [DataMember(Order = 1, Name = "skip"), JsonPropertyName("skip"), YamlMember(Alias = "skip")]
    public virtual bool SkipValidation { get; set; }

    /// <summary>
    /// Gets/sets the strategy to use when validating inbound cloud events<para></para>
    /// Defaults to '<see cref="CloudEventValidationStrategy.Fail"/>'
    /// </summary>
    [DefaultValue(CloudEventValidationStrategy.Fail)]
    [DataMember(Order = 2, Name = "validationStrategy"), JsonPropertyName("validationStrategy"), YamlMember(Alias = "validationStrategy")]
    public virtual string ValidationStrategy { get; set; } = CloudEventValidationStrategy.Fail;

    /// <summary>
    /// Gets/sets an object used to configure the JSON schema based validation of incoming cloud events
    /// </summary>
    [DataMember(Order = 3, Name = "dataSchema"), JsonPropertyName("dataSchema"), YamlMember(Alias = "dataSchema")]
    public virtual CloudEventDataSchemaValidationPolicy? DataSchema { get; set; }

}
