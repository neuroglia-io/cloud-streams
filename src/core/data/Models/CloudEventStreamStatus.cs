namespace CloudStreams.Core.Data.Models;

/// <summary>
/// Represents an object used to describe the status of a cloud event stream
/// </summary>
[DataContract]
public class CloudEventStreamStatus
{

    /// <summary>
    /// Gets/sets the acked offset in the cloud event stream starting from which to receive events
    /// </summary>
    [DataMember(Order = 1, Name = "ackedOffset"), JsonPropertyName("ackedOffset"), YamlMember(Alias = "ackedOffset")]
    public virtual ulong? AckedOffset { get; set; }

}