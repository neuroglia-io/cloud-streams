namespace CloudStreams.Core.Data.Models;

/// <summary>
/// Represents an object used to configure a webhook
/// </summary>
[DataContract]
public class Webhook
{

    /// <summary>
    /// Gets/sets the address of the service to post back to
    /// </summary>
    [Required, JsonRequired]
    [DataMember(Order = 1, Name = "serviceUri", IsRequired = true), JsonPropertyName("serviceUri"), YamlMember(Alias = "serviceUri")]
    public virtual Uri ServiceUri { get; set; } = null!;

}
