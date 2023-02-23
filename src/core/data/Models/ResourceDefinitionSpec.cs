namespace CloudStreams.Core.Data.Models;

/// <summary>
/// Represents an object used to configure a resource definition
/// </summary>
[DataContract]
public class ResourceDefinitionSpec
{

    /// <summary>
    /// Gets/sets a list containing object used to describe the resource definition's versions
    /// </summary>
    [Required, JsonRequired, MinLength(1)]
    [DataMember(Order = 1, Name = "versions", IsRequired = true), JsonPropertyName("versions"), YamlMember(Alias = "versions")]
    public virtual List<ResourceDefinitionVersion> Versions { get; set; } = null!;

}
