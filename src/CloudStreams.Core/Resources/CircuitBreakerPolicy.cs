namespace CloudStreams.Core.Resources;

/// <summary>
/// Represents an object used to configure a circuit breaker
/// </summary>
[DataContract]
public record CircuitBreakerPolicy
{

    /// <summary>
    /// Initializes a new <see cref="CircuitBreakerPolicy"/>
    /// </summary>
    public CircuitBreakerPolicy() { }

    /// <summary>
    /// Initializes a new <see cref="CircuitBreakerPolicy"/>
    /// </summary>
    /// <param name="breakAfter">The maximum attempts after which to break the circuit</param>
    /// <param name="breakDuration">The duration the circuit remains broker</param>
    public CircuitBreakerPolicy(int breakAfter, Duration breakDuration)
    {
        ArgumentNullException.ThrowIfNull(breakDuration);

        this.BreakAfter = breakAfter;
        this.BreakDuration = breakDuration;
    }

    /// <summary>
    /// Gets/sets the maximum attempts after which to break the circuit
    /// </summary>
    [Required]
    [DataMember(Order = 1, Name = "breakAfter", IsRequired = true), JsonPropertyOrder(1), JsonPropertyName("breakAfter"), YamlMember(Order = 1, Alias = "breakAfter")]
    public virtual int BreakAfter { get; set; }

    /// <summary>
    /// Gets/sets the duration the circuit remains broken
    /// </summary>
    [Required]
    [DataMember(Order = 2, Name = "breakDuration", IsRequired = true), JsonPropertyOrder(2), JsonPropertyName("breakDuration"), YamlMember(Order = 2, Alias = "breakDuration")]
    public virtual Duration BreakDuration { get; set; } = null!;

}