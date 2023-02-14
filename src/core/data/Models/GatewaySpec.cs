namespace CloudStreams.Data.Models;

/// <summary>
/// Represents an object used to configure a cloud event gateway
/// </summary>
[DataContract]
public class GatewaySpec
{

    /// <summary>
    /// Gets/sets the authorization policy that applies to cloud events of any source
    /// </summary>
    [DataMember(Order = 1, Name = "authorization"), JsonPropertyName("authorization"), YamlMember(Alias = "authorization")]
    public virtual CloudEventAuthorizationPolicy? Authorization { get; set; } = null!;

    /// <summary>
    /// Gets/sets the validation policy that applies to cloud events of any source
    /// </summary>
    [DataMember(Order = 2, Name = "validation"), JsonPropertyName("validation"), YamlMember(Alias = "validation")]
    public virtual CloudEventValidationPolicy? Validation { get; set; } = null!;

    /// <summary>
    /// Gets/sets the configuration that applies to specific cloud event sources
    /// </summary>
    [DataMember(Order = 3, Name = "sources"), JsonPropertyName("sources"), YamlMember(Alias = "sources")]
    public virtual List<CloudEventSourceSpec>? Sources { get; set; }

}
