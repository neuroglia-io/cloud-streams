namespace CloudStreams.Core.Data.Models;

/// <summary>
/// Represents the definition of a resource type
/// </summary>
[DataContract]
public class ResourceDefinition
    : IResourceDefinition
{

    /// <inheritdoc/>
    [Required, JsonRequired]
    [DataMember(Order = 1, Name = "metadata", IsRequired = true), JsonPropertyName("metadata"), YamlMember(Alias = "metadata")]
    public ResourceMetadata Metadata { get; set; } = null!;

    /// <inheritdoc/>
    [Required, JsonRequired]
    [DataMember(Order = 2, Name = "spec", IsRequired = true), JsonPropertyName("spec"), YamlMember(Alias = "spec")]
    public ResourceDefinitionSpec Spec { get; set; } = null!;

    object IMetadata.Metadata => this.Metadata;

    object ISpec.Spec => this.Spec;

}
