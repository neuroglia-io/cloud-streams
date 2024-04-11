namespace CloudStreams.Core.Resources;

/// <summary>
/// Represents an object used to configure a cloud event broker
/// </summary>
[DataContract]
public record BrokerSpec
{

    /// <summary>
    /// Initializes a new <see cref="BrokerSpec"/>
    /// </summary>
    public BrokerSpec() { }

    /// <summary>
    /// Initializes a new <see cref="BrokerSpec"/>
    /// </summary>
    /// <param name="dispatch">An object used to configure the way the broker should dispatch cloud events</param>
    public BrokerSpec(BrokerDispatchConfiguration dispatch)
    {
        this.Dispatch = dispatch;
    }

    /// <summary>
    /// Gets/sets an object used to configure the way the broker should dispatch cloud events
    /// </summary>
    [DataMember(Order = 1, Name = "dispatch"), JsonPropertyOrder(1), JsonPropertyName("dispatch"), YamlMember(Order = 1, Alias = "dispatch")]
    public virtual BrokerDispatchConfiguration? Dispatch { get; set; }

    /// <summary>
    /// Gets/sets a key/value mapping of the labels to select subscriptions by.<para></para>
    /// If not set, the broker will attempt to pick up all inactive subscriptions
    /// </summary>
    [DataMember(Order = 2, Name = "selector"), JsonPropertyOrder(2), JsonPropertyName("selector"), YamlMember(Order = 2, Alias = "selector")]
    public virtual IDictionary<string, string>? Selector { get; set; }

    /// <summary>
    /// Gets/sets an object used to configure the broker service, if any
    /// </summary>
    [DataMember(Order = 3, Name = "service"), JsonPropertyOrder(3), JsonPropertyName("service"), YamlMember(Order = 3, Alias = "service")]
    public virtual ServiceConfiguration? Service { get; set; }

}
