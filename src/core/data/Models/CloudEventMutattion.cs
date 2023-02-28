namespace CloudStreams.Core.Data.Models;

/// <summary>
/// Represents an object used to configure how to mutate consumed cloud events
/// </summary>
[DataContract]
public class CloudEventMutattion
{

    /// <summary>
    /// Gets/sets a runtime expression string or object used to mutate consumed cloud events
    /// </summary>
    [Required, JsonRequired]
    [DataMember(Order = 1, Name = "expression"), JsonPropertyName("expression"), YamlMember(Alias = "expression")]
    public virtual object Expression { get; set; } = null!;

    /// <summary>
    /// Gets/sets a runtime expression used to determine whether or not the configured mutation should occur. If not set, the mutation always occurs.
    /// </summary>
    [DataMember(Order = 2, Name = "condition"), JsonPropertyName("condition"), YamlMember(Alias = "condition")]
    public virtual string? Condition { get; set; } = null!;

}