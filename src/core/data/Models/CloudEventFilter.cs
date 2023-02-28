namespace CloudStreams.Core.Data.Models;

/// <summary>
/// Represents an object used to configure how to filter consumed cloud events
/// </summary>
[DataContract]
public class CloudEventFilter
{

    /// <summary>
    /// Gets/sets a key/value mapping of the attributes to filter cloud events by. Keys support regular expressions, and values support runtime expressions
    /// </summary>
    [Required, JsonRequired, MinLength(1)]
    public virtual Dictionary<string, string> Attributes { get; set; } = null!;

}
