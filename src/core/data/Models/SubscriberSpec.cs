namespace CloudStreams.Core.Data.Models;

/// <summary>
/// Represents an object used to configure a cloud event subscription's consumer
/// </summary>
[DataContract]
public class SubscriberSpec
{

    /// <summary>
    /// Gets/sets the subscriber service's address
    /// </summary>
    [DataMember(Order = 1, Name = "serviceAddress"), JsonPropertyName("serviceAddress")]
    public virtual Uri ServiceAddress { get; set; } = null!;

}