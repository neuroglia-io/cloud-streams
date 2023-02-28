namespace CloudStreams.Core.Data.Models;

/// <summary>
/// Represents an object used to configure how to mutate consumed cloud events
/// </summary>
[DataContract]
public class CloudEventMutation
{

    /// <summary>
    /// Gets/sets the mutation strategy to use
    /// </summary>
    [DataMember(Order = 1, Name = "strategy"), JsonPropertyName("strategy"), YamlMember(Alias = "strategy")]
    public virtual CloudEventMutationStrategy Strategy { get; set; }

    /// <summary>
    /// Gets/sets the runtime expression string or object used to mutate consumed cloud events. 
    /// Required if 'strategy' has been set to 'expression'
    /// </summary>
    [DataMember(Order = 2, Name = "expression"), JsonPropertyName("expression"), YamlMember(Alias = "expression")]
    public virtual object? Expression { get; set; }

    /// <summary>
    /// Gets/sets the runtime expression string or object used to mutate consumed cloud events. 
    /// Required if 'strategy' has been set to 'webhook'
    /// </summary>
    [DataMember(Order = 3, Name = "webhook"), JsonPropertyName("webhook"), YamlMember(Alias = "webhook")]
    public virtual Webhook? Webhook { get; set; }

}
