namespace CloudStreams.Core.Data.Models;

/// <summary>
/// Represents an object used to configure a cloud event subscription
/// </summary>
[DataContract]
public class SubscriptionSpec
{

    /// <summary>
    /// Initializes a new <see cref="SubscriptionSpec"/>
    /// </summary>
    public SubscriptionSpec() { }

    /// <summary>
    /// Initializes a new <see cref="SubscriberSpec"/>
    /// </summary>
    /// <param name="subscribers">A list containing objects describing the subscription's consumers</param>
    /// <param name="filter">An object used to configure how to mutate consumed cloud events</param>
    /// <param name="mutation">An object used to configure how to mutate consumed cloud events</param>
    /// <param name="stream">An object used to configure the subscription's cloud event stream</param>
    public SubscriptionSpec(IEnumerable<SubscriberSpec> subscribers, CloudEventFilter? filter, CloudEventMutattion? mutation, CloudEventStreamSpec? stream)
    {
        if (this.Subscribers == null || !subscribers.Any()) throw new ArgumentNullException(nameof(subscribers));
        this.Subscribers = subscribers.ToList();
        this.Filter = filter;
        this.Mutation = mutation;
        this.Stream = stream;
    }

    /// <summary>
    /// Gets/sets a list containing objects describing the subscription's consumers
    /// </summary>
    [Required, JsonRequired, MinLength(1)]
    [DataMember(Order = 1, Name = "subscribers"), JsonPropertyName("subscribers"), YamlMember(Alias = "subscribers")]
    public virtual List<SubscriberSpec> Subscribers { get; set; } = null!;

    /// <summary>
    /// Gets/sets an object used to configure how to mutate consumed cloud events
    /// </summary>
    [DataMember(Order = 2, Name = "filter"), JsonPropertyName("filter"), YamlMember(Alias = "filter")]
    public virtual CloudEventFilter? Filter { get; set; }

    /// <summary>
    /// Gets/sets an object used to configure how to mutate consumed cloud events
    /// </summary>
    [DataMember(Order = 3, Name = "mutation"), JsonPropertyName("mutation"), YamlMember(Alias = "mutation")]
    public virtual CloudEventMutattion? Mutation { get; set; }

    /// <summary>
    /// Gets/sets an object used to configure the subscription's cloud event stream
    /// </summary>
    [DataMember(Order = 4, Name = "stream"), JsonPropertyName("stream"), YamlMember(Alias = "stream")]
    public virtual CloudEventStreamSpec? Stream { get; set; }

}
