namespace CloudStreams.Core.Data.Models;

/// <summary>
/// Represents a reference to a resource
/// </summary>
[DataContract]
public class ResourceReference
    : IResourceReference
{

    /// <inheritdoc/>
    [Required, JsonRequired]
    [DataMember(Order = 1, Name = "apiVersion", IsRequired = true), JsonPropertyName("apiVersion"), YamlMember(Alias = "apiVersion")]
    public virtual string ApiVersion { get; set; } = null!;

    /// <inheritdoc/>
    [Required, JsonRequired]
    [DataMember(Order = 2, Name = "kind", IsRequired = true), JsonPropertyName("kind"), YamlMember(Alias = "kind")]
    public virtual string Kind { get; set; } = null!;

    /// <inheritdoc/>
    [Required, JsonRequired]
    [DataMember(Order = 3, Name = "name", IsRequired = true), JsonPropertyName("name"), YamlMember(Alias = "name")]
    public virtual string Name { get; set; } = null!;

    /// <inheritdoc/>
    [DataMember(Order = 4), JsonPropertyName("namespace"), YamlMember(Alias = "namespace")]
    public virtual string? Namespace { get; set; }

}

/// <summary>
/// Represents a reference to a resource
/// </summary>
/// <typeparam name="TResource">The type of the reference <see cref="IResource"/></typeparam>
[DataContract]
public class ResourceReference<TResource>
    : IResourceReference
    where TResource : class, IResource, new()
{

    private static readonly TResource Resource = new();

    string IResourceReference.ApiVersion => Resource.ApiVersion;

    string IResourceReference.Kind => Resource.Kind;

    /// <inheritdoc/>
    [Required, JsonRequired]
    [DataMember(Order = 1, Name = "name", IsRequired = true), JsonPropertyName("name"), YamlMember(Alias = "name")]
    public virtual string Name { get; set; } = null!;

    /// <inheritdoc/>
    [DataMember(Order = 2), JsonPropertyName("namespace"), YamlMember(Alias = "namespace")]
    public virtual string? Namespace { get; set; }



}