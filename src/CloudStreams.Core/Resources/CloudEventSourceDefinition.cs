namespace CloudStreams.Core.Resources;

/// <summary>
/// Represents an object used to configure a cloud event source
/// </summary>
[DataContract]
public record CloudEventSourceDefinition
{

    /// <summary>
    /// Gets/sets the uri of the cloud event source to configure
    /// </summary>
    [Required, JsonRequired]
    [DataMember(Order = 1, Name = "uri", IsRequired = true), JsonPropertyName("uri"), YamlMember(Alias = "uri")]
    public virtual Uri Uri { get; set; } = null!;

    /// <summary>
    /// Gets/sets the policy to use to authorize cloud events produced by the source
    /// </summary>
    [DataMember(Order = 2, Name = "authorization"), JsonPropertyName("authorization"), YamlMember(Alias = "authorization")]
    public virtual CloudEventAuthorizationPolicy? Authorization { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [DataMember(Order = 3, Name = "validation"), JsonPropertyName("validation"), YamlMember(Alias = "validation")]
    public virtual CloudEventValidationPolicy? Validation { get; set; }

}