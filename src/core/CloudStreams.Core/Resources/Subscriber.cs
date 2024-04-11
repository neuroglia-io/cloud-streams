namespace CloudStreams.Core.Resources;

/// <summary>
/// Represents an object used to configure the consumer of cloud events produced by a subscription
/// </summary>
[DataContract]
public record Subscriber
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
    [DataMember(Order = 2, Name = "rateLimit"), JsonPropertyName("rateLimit"), YamlMember(Alias = "rateLimit")]
    public virtual double? RateLimit { get; set; }

    /// <summary>
    /// Gets/sets the retry policy to use when dispatching cloud events to the subscriber. If not set, will fallback to the broker's default retry policy
    /// </summary>
    [DataMember(Order = 3, Name = "retryPolicy"), JsonPropertyName("retryPolicy"), YamlMember(Alias = "retryPolicy")]
    public virtual HttpClientRetryPolicy? RetryPolicy { get; set; }

}