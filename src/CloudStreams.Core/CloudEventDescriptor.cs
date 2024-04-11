namespace CloudStreams.Core;

/// <summary>
/// Represents an object used to describe a cloud event
/// </summary>
[DataContract]
public record CloudEventDescriptor
{

    /// <summary>
    /// Initializes a new <see cref="CloudEventDescriptor"/>
    /// </summary>
    public CloudEventDescriptor() { }

    /// <summary>
    /// Initializes a new <see cref="CloudEventDescriptor"/>
    /// </summary>
    /// <param name="metadata">An recorded cloud event's metadata</param>
    /// <param name="data">The </param>
    public CloudEventDescriptor(CloudEventMetadata metadata, object? data = null)
    {
        this.Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        this.Data = data;
    }

    /// <summary>
    /// Gets/sets the recorded cloud event's metadata
    /// </summary>
    [DataMember(Order = 1, Name = "metadata"), JsonPropertyName("metadata"), YamlMember(Alias = "metadata")]
    public virtual CloudEventMetadata Metadata { get; set; } = null!;


    /// <summary>
    /// Gets/sets the cloud event's data
    /// </summary>
    [DataMember(Order = 2, Name = "data"), JsonPropertyName("data"), YamlMember(Alias = "data")]
    public virtual object? Data { get; set; } = null!;

}
