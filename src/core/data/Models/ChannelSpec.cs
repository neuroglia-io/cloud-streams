namespace CloudStreams.Core.Data.Models;

/// <summary>
/// Represents an object used to configure a <see cref="Channel"/>
/// </summary>
[DataContract]
public class ChannelSpec
{

    /// <summary>
    /// Gets/sets the channel service's address
    /// </summary>
    [DataMember(Order = 1, Name = "serviceAddress"), JsonPropertyName("serviceAddress")]
    public virtual Uri ServiceAddress { get; set; } = null!;

    /// <summary>
    /// Gets/sets the channel's cloud event stream
    /// </summary>
    [DataMember(Order = 2, Name = "stream"), JsonPropertyName("stream")]
    public virtual CloudEventStreamSpec? Stream { get; set; }

}
