namespace CloudStreams.Core.Resources;

/// <summary>
/// Represents an object used to configure how to mutate consumed cloud events
/// </summary>
[DataContract]
public record CloudEventMutation
{

    /// <summary>
    /// Gets/sets the mutation strategy to use
    /// </summary>
    [DataMember(Order = 1, Name = "type"), JsonPropertyName("type"), YamlMember(Alias = "type")]
    public virtual CloudEventMutationType Type { get; set; }

    /// <summary>
    /// Gets/sets the runtime expression string or object used to mutate consumed cloud events. 
    /// Required if 'type' has been set to 'expression'
    /// </summary>
    [DataMember(Order = 2, Name = "expression"), JsonPropertyName("expression"), YamlMember(Alias = "expression")]
    public virtual object? Expression { get; set; }

    /// <summary>
    /// Gets/sets an object used to configure the webhook request to perform in order to mutate the cloud event.
    /// Required if 'type' has been set to 'webhook'
    /// </summary>
    [DataMember(Order = 3, Name = "webhook"), JsonPropertyName("webhook"), YamlMember(Alias = "webhook")]
    public virtual Webhook? Webhook { get; set; }

}
