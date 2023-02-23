namespace CloudStreams.Core.Data.Models;

/// <summary>
/// Represents the object used to configure a schema to validate defined resources
/// </summary>
[DataContract]
public class ResourceDefinitionValidation
{

    /// <summary>
    /// Gets/sets the JSON schema used to validate defined resources
    /// </summary>
    [Required, JsonRequired]
    [DataMember(Order = 1, Name = "openAPIV3Schema", IsRequired = true), JsonPropertyName("openAPIV3Schema"), YamlMember(Alias = "openAPIV3Schema")]
    public JsonSchema OpenAPIV3Schema { get; set; } = null!;

}