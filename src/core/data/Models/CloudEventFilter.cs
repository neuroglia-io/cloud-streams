namespace CloudStreams.Core.Data.Models;

/// <summary>
/// Represents an object used to configure a cloud event filter
/// </summary>
[DataContract]
public class CloudEventFilter
{

    /// <summary>
    /// Gets/sets the filtering strategy to use
    /// </summary>
    [DataMember(Order = 1, Name = "strategy"), JsonPropertyName("strategy"), YamlMember(Alias = "strategy")]
    public virtual CloudEventFilteringStrategy Strategy { get; set; }

    /// <summary>
    /// Gets/sets a key/value mapping of the context attributes by which to filter consumed cloud events.
    /// Required if 'strategy' has been set to 'attributes'
    /// Values support regular and runtime expressions. 
    /// If no value has been supplied for a given key, it will match cloud events that define said attribute, no matter its value
    /// </summary>
    [DataMember(Order = 2, Name = "attributes"), JsonPropertyName("attributes"), YamlMember(Alias = "attributes")]
    public virtual IDictionary<string, string>? Attributes { get; set; } = null!;

    /// <summary>
    /// Gets/sets the runtime expression based condition to evaluate consumed cloud events against
    /// Required if 'strategy' has been set to 'expression'
    /// </summary>
    [DataMember(Order = 2, Name = "expression"), JsonPropertyName("expression"), YamlMember(Alias = "expression")]
    public virtual string? Expression { get; set; }

}
