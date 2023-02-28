namespace CloudStreams.Core.Data.Models;

/// <summary>
/// Represents an object used to configure the consumer of cloud events produced by a subscription
/// </summary>
[DataContract]
public class Subscriber
{

    /// <summary>
    /// Gets/sets the address of the dispatch consumed cloud events to
    /// </summary>
    [Required, JsonRequired]
    [DataMember(Order = 1, Name = "serviceUri", IsRequired = true), JsonPropertyName("serviceUri"), YamlMember(Alias = "serviceUri")]
    public virtual Uri ServiceUri { get; set; } = null!;

}