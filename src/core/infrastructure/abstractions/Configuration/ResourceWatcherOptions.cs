namespace CloudStreams.Infrastructure.Configuration;

/// <summary>
/// Represents an object used to configure an <see cref="Services.IResourceWatcher{TResource}"/>
/// </summary>
[DataContract]
public class ResourceWatcherOptions
{

    /// <summary>
    /// Gets/sets the namespace the resources to watch belong to
    /// </summary>
    [DataMember(Order = 1, Name = "namespace"), JsonPropertyName("namespace"), YamlMember(Alias = "namespace")]
    public virtual string? Namespace { get; set; }

}