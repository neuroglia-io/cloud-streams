namespace CloudStreams.Core;

/// <summary>
/// Represents an object used to describe a recorded cloud event
/// </summary>
[DataContract]
public record CloudEventRecord
    : CloudEventDescriptor
{

    /// <summary>
    /// Initializes a new <see cref="CloudEventRecord"/>
    /// </summary>
    public CloudEventRecord() { }

    /// <summary>
    /// Initializes a new <see cref="CloudEventRecord"/>
    /// </summary>
    /// <param name="streamId">The id of the stream the recorded cloud event belongs to</param>
    /// <param name="sequence">The sequence of the recorded cloud event in the stream it belongs to</param>
    /// <param name="metadata">An object used to describe the recorded cloud event</param>
    /// <param name="data">The recorded cloud event's data</param>
    public CloudEventRecord(string streamId, ulong sequence, CloudEventMetadata metadata, object? data)
        : base(metadata, data)
    {
        if (string.IsNullOrWhiteSpace(streamId)) throw new ArgumentNullException(nameof(streamId));
        this.StreamId = streamId;
        this.Sequence = sequence;
    }

    /// <summary>
    /// Gets/sets the id of the stream the recorded cloud event belongs to
    /// </summary>
    [DataMember(Order = 1, Name = "streamId"), JsonPropertyName("streamId"), YamlMember(Alias = "streamId")]
    public virtual string StreamId { get; set; } = null!;

    /// <summary>
    /// Gets/sets the sequence of the recorded cloud event in the stream it belongs to
    /// </summary>
    [DataMember(Order = 2, Name = "sequence"), JsonPropertyName("sequence"), YamlMember(Alias = "sequence")]
    public virtual ulong Sequence { get; set; }

}