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
    /// Gets/sets an object used to configure the subscription's cloud event stream
    /// </summary>
    [DataMember(Order = 2, Name = "stream"), JsonPropertyName("stream"), YamlMember(Alias = "stream")]
    public virtual CloudEventStreamSpec? Stream { get; set; }

}