namespace CloudStreams.Core.Data.Models;

/// <summary>
/// Describes a patch that applies to a resource
/// </summary>
[DataContract]
public class ResourcePatch
{

    /// <summary>
    /// Gets/sets a reference to the resource to patch
    /// </summary>
    [Required, JsonRequired]
    [DataMember(Order = 1, Name = "resource", IsRequired = true), JsonPropertyName("resource"), YamlMember(Alias = "resource")]
    public virtual ResourceReference Resource { get; set; } = null!;

    /// <summary>
    /// Gets/sets the patch to apply
    /// </summary>
    [Required, JsonRequired]
    [DataMember(Order = 2, Name = "patch", IsRequired = true), JsonPropertyName("patch"), YamlMember(Alias = "patch")]
    public virtual Patch Patch { get; set; } = null!;

}

/// <summary>
/// Describes a patch that applies to a resource
/// </summary>
[DataContract]
public class ResourcePatch<TResource>
    where TResource : class, IResource, new()
{

    /// <summary>
    /// Gets/sets a reference to the resource to patch
    /// </summary>
    [Required, JsonRequired]
    [DataMember(Order = 1, Name = "resource", IsRequired = true), JsonPropertyName("resource"), YamlMember(Alias = "resource")]
    public virtual ResourceReference<TResource> Resource { get; set; } = null!;

    /// <summary>
    /// Gets/sets the patch to apply
    /// </summary>
    [Required, JsonRequired]
    [DataMember(Order = 2, Name = "patch", IsRequired = true), JsonPropertyName("patch"), YamlMember(Alias = "patch")]
    public virtual Patch Patch { get; set; } = null!;

}