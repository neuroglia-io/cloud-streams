namespace CloudStreams.Core.Resources;

/// <summary>
/// Represents an object used to describe the status of a cloud event subscription
/// </summary>
[DataContract]
public record SubscriptionStatus
{

    /// <summary>
    /// Gets/sets the status phase of the described subscription
    /// </summary>
    [Required, DefaultValue(SubscriptionStatusPhase.Inactive)]
    [DataMember(Order = 1, Name = "phase"), JsonPropertyName("phase"), YamlMember(Alias = "phase")]
    public virtual SubscriptionStatusPhase Phase { get; set; }

    /// <summary>
    /// Gets/sets the observed generation of the subscription's spec the status describes. Divergence between resource and observed generation values should be handled during a reconciliation loop
    /// </summary>
    [DataMember(Order = 2, Name = "observedGeneration"), JsonPropertyName("observedGeneration"), YamlMember(Alias = "observedGeneration")]
    public virtual ulong? ObservedGeneration { get; set; }

    /// <summary>
    /// Gets/sets an object used to describe the status of the subscription's cloud event stream
    /// </summary>
    [DataMember(Order = 3, Name = "stream"), JsonPropertyName("stream"), YamlMember(Alias = "stream")]
    public virtual CloudEventStreamStatus? Stream { get; set; }

}