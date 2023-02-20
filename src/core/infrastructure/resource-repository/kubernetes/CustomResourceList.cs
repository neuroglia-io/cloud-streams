using k8s;
using k8s.Models;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using YamlDotNet.Serialization;

namespace CloudStreams.Core.Infrastructure.Services;

/// <summary>
/// Represents a list of custom objects
/// </summary>
/// <typeparam name="TResource">The type resources contained by the list</typeparam>
[DataContract]
public class CustomResourceList<TResource>
    : IItems<TResource>
    where TResource : class, IResource, new()
{

    /// <summary>
    /// Gets/sets the api version of the listed resource
    /// </summary>
    [DataMember(Order = 1, Name = "apiVersion"), JsonPropertyName("apiVersion"), YamlMember(Alias = "apiVersion")]
    public virtual string ApiVersion { get; set; } = null!;

    /// <summary>
    /// Gets/sets the kind version of the listed resource
    /// </summary>
    [DataMember(Order = 2, Name = "kind"), JsonPropertyName("kind"), YamlMember(Alias = "kind")]
    public virtual string Kind { get; set; } = null!;

    /// <summary>
    /// Gets/sets the list's metadata
    /// </summary>
    [DataMember(Order = 3, Name = "metadata"), JsonPropertyName("metadata"), YamlMember(Alias = "metadata")]
    public virtual V1ListMeta Metadata { get; set; } = null!;

    /// <summary>
    /// Gets/sets the items the list is made out of
    /// </summary>
    [DataMember(Order = 4, Name = "items"), JsonPropertyName("items"), YamlMember(Alias = "items")]
    public virtual IList<TResource> Items { get; set; } = null!;

}