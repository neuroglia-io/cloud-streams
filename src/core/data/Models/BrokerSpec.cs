namespace CloudStreams.Core.Data.Models;

/// <summary>
/// Represents an object used to configure a cloud event broker
/// </summary>
[DataContract]
public class BrokerSpec
{

    /// <summary>
    /// Initializes a new <see cref="BrokerSpec"/>
    /// </summary>
    public BrokerSpec() { }

    /// <summary>
    /// Initializes a new <see cref="BrokerSpec"/>
    /// </summary>
    /// <param name="network">The name of the network the broker belongs to</param>
    public BrokerSpec(string network)
    {
        if (string.IsNullOrWhiteSpace(network)) throw new ArgumentNullException(nameof(network));
        this.Network = network;
    }

    /// <summary>
    /// Gets/sets the name of the network the broker belongs to
    /// </summary>
    [Required, JsonRequired]
    [DataMember(Order = 1, Name = "network", IsRequired = true), JsonPropertyName("network"), YamlMember(Alias = "network")]
    public virtual string Network { get; set; } = null!;

    /// <summary>
    /// Gets/sets an object used to configure the broker's cloud event stream
    /// </summary>
    [DataMember(Order = 2, Name = "stream"), JsonPropertyName("stream"), YamlMember(Alias = "stream")]
    public virtual CloudEventStream? Stream { get; set; }

}
