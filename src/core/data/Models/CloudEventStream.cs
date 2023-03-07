namespace CloudStreams.Core.Data.Models;

/// <summary>
/// Represents an object used to configure a cloud event stream
/// </summary>
[DataContract]
public class CloudEventStream
{

    /// <summary>
    /// Gets/sets the desired offset in the cloud event stream starting from which to receive events.<para></para>
    /// '0' specifies the start of the stream, '-1' the end of the stream. Defaults to '-1'
    /// </summary>
    [DataMember(Order = 1, Name = "offset"), JsonPropertyName("offset"), YamlMember(Alias = "offset")]
    public virtual long? Offset { get; set; }

}