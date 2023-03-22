namespace CloudStreams.Core.Data.Models;

/// <summary>
/// Represents an object used to describe a version of a resource definition
/// </summary>
[DataContract]
public class ResourceDefinitionVersion
{

    /// <summary>
    /// Gets/sets the name of the described resource definition version
    /// </summary>
    [Required, JsonRequired]
    [DataMember(Order = 1, Name = "name", IsRequired = true), JsonPropertyName("name"), YamlMember(Alias = "name")]
    public virtual string Name { get; set; } = null!;

    /// <summary>
    /// Gets/sets an object used to configure a schema to validate defined resources
    /// </summary>
    [Required, JsonRequired]
    [DataMember(Order = 2, Name = "schema", IsRequired = true), JsonPropertyName("schema"), YamlMember(Alias = "schema")]
    public virtual ResourceDefinitionValidation Schema { get; set; } = null!;

}
