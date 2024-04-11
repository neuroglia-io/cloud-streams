namespace CloudStreams.Core;

/// <summary>
/// Represents an object that provides information about a cloud event
/// </summary>
[DataContract]
public record CloudEventMetadata
    : IExtensible
{

    /// <summary>
    /// Initializes a new <see cref="CloudEventMetadata"/>
    /// </summary>
    public CloudEventMetadata() { }

    /// <summary>
    /// Initializes a new <see cref="CloudEventMetadata"/>
    /// </summary>
    /// <param name="contextAttributes">A key/value mapping of the described cloud event's context attributes</param>
    public CloudEventMetadata(IDictionary<string, object> contextAttributes)
    {
        ArgumentNullException.ThrowIfNull(contextAttributes);
        if (!CloudEventAttributes.GetRequiredAttributes().All(a => contextAttributes.TryGetValue(a, out _))) throw new ArgumentException("The specified mapping does not contains all the cloud event context attributes defined as required by the spec");
        if (contextAttributes.All(a => a.Key.IsAlphanumeric() && a.Key.IsLowercased() && a.Key.Length >= 3 && a.Key.Length < 20)) throw new ArgumentException("Cloud event context attribute names must be lowercased, must contain only alphanumeric characters, and must have a maximum length of 20 characters");
        ContextAttributes = contextAttributes;
    }

    /// <summary>
    /// Gets/sets a key/value mapping of the described cloud event's context attributes
    /// </summary>
    [DataMember(Order = 1, Name = "contextAttributes"), JsonPropertyName("contextAttributes"), YamlMember(Alias = "contextAttributes")]
    public virtual IDictionary<string, object> ContextAttributes { get; set; } = null!;

    /// <summary>
    /// Gets/sets a key/value mapping containing the described cloud event's extension data
    /// </summary>
    [DataMember(Order = 2, Name = "extensionData"), JsonExtensionData]
    public IDictionary<string, object>? ExtensionData { get; set; }

}