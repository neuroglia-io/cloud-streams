namespace CloudStreams.Core.Resources;

/// <summary>
/// Represents an object used to configure a cloud event subscription
/// </summary>
[DataContract]
public record SubscriptionSpec
{

    /// <summary>
    /// Initializes a new <see cref="SubscriptionSpec"/>
    /// </summary>
    public SubscriptionSpec() { }

    /// <summary>
    /// Gets/sets an object used to reference the partition to subscribe to, if any.
    /// If none has been set, the subscription receives all cloud events, regardless of their source, type or subject
    /// </summary>
    [DataMember(Order = 1, Name = "partition"), JsonPropertyName("partition"), YamlMember(Alias = "partition")]
    public virtual PartitionReference? Partition { get; set; }

    /// <summary>
    /// Gets/sets an object used to configure how to filter consumed cloud events
    /// </summary>
    [DataMember(Order = 2, Name = "filter"), JsonPropertyName("filter"), YamlMember(Alias = "filter")]
    public virtual CloudEventFilter? Filter { get; set; }

    /// <summary>
    /// Gets/sets an object used to configure how to mutate consumed cloud events 
    /// </summary>
    [DataMember(Order = 3, Name = "mutation"), JsonPropertyName("mutation"), YamlMember(Alias = "mutation")]
    public virtual CloudEventMutation? Mutation { get; set; }

    /// <summary>
    /// Gets/sets an object used to configure the subscription's cloud event stream
    /// </summary>
    [DataMember(Order = 4, Name = "stream"), JsonPropertyName("stream"), YamlMember(Alias = "stream")]
    public virtual CloudEventStream? Stream { get; set; }

    /// <summary>
    /// Gets/sets an object used to configure the service to dispatch cloud events consumed by the subscription
    /// </summary>
    [Required, JsonRequired]
    [DataMember(Order = 5, Name = "subscriber", IsRequired = true), JsonPropertyName("subscriber"), YamlMember(Alias = "subscriber")]
    public virtual Subscriber Subscriber { get; set; } = null!;

}