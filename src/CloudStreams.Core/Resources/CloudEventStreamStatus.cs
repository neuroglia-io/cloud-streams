namespace CloudStreams.Core.Resources;

/// <summary>
/// Represents an object used to describe the status of a cloud event stream
/// </summary>
[DataContract]
public record CloudEventStreamStatus
{

    /// <summary>
    /// Gets/sets the acked offset in the cloud event stream starting from which to receive events
    /// </summary>
    [DataMember(Order = 1, Name = "ackedOffset"), JsonPropertyName("ackedOffset"), YamlMember(Alias = "ackedOffset")]
    public virtual ulong? AckedOffset { get; set; }

    /// <summary>
    /// Gets/sets an object that describes the last fault that occurred while streaming events to subscribers. Streaming is interrupted when fault is set, requiring a user to manually resume streaming
    /// </summary>
    [DataMember(Order = 2, Name = "fault"), JsonPropertyName("fault"), YamlMember(Alias = "fault")]
    public virtual ProblemDetails? Fault { get; set; }

}