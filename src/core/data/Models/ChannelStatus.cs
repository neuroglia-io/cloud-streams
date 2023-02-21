namespace CloudStreams.Core.Data.Models;

/// <summary>
/// Represents an object used to describe a <see cref="Channel"/>'s status
/// </summary>
[DataContract]
public class ChannelStatus
{

    /// <summary>
    /// Gets/sets the observed generation of the broker's spec the status describes. Divergence between resource and observed generation values should be handled during a reconciliation loop
    /// </summary>
    [DataMember(Order = 1, Name = "observedGeneration"), JsonPropertyName("observedGeneration"), YamlMember(Alias = "observedGeneration")]
    public virtual ulong ObservedGeneration { get; set; }

    /// <summary>
    /// Gets/sets the status of the channel's cloud event stream
    /// </summary>
    [DataMember(Order = 2, Name = "stream"), JsonPropertyName("stream")]
    public virtual CloudEventStreamStatus? Stream { get; set; }

}
