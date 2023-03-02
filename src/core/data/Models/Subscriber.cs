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
    [DataMember(Order = 1, Name = "uri", IsRequired = true), JsonPropertyName("uri"), YamlMember(Alias = "uri")]
    public virtual Uri Uri { get; set; } = null!;

    /// <summary>
    /// Gets/sets the maximum amount of events, if any, that can be dispatched per second to the subscriber
    /// </summary>
    [Required, JsonRequired]
    [DataMember(Order = 2, Name = "rateLimit", IsRequired = true), JsonPropertyName("rateLimit"), YamlMember(Alias = "rateLimit")]
    public virtual double? RateLimit { get; set; }

}