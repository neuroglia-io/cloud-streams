namespace CloudStreams.Core.Resources;

/// <summary>
/// Represents an object used to configure a retry backoff duration
/// </summary>
[DataContract]
public record RetryBackoffDuration
{

    /// <summary>
    /// Gets/sets the duration's type
    /// </summary>
    [Required]
    [DataMember(Order = 1, Name = "type", IsRequired = true), JsonPropertyName("type"), YamlMember(Alias = "type")]
    public virtual RetryBackoffDurationType Type { get; set; }

    /// <summary>
    /// Gets/sets the period of time to wait between retry attempts 
    /// </summary>
    [Required]
    [DataMember(Order = 2, Name = "period", IsRequired = true), JsonPropertyName("period"), YamlMember(Alias = "period")]
    public virtual Duration Period { get; set; } = null!;

    /// <summary>
    /// Gets/sets a value representing the power to which the specified period of time is to be raised to obtain the time to wait between each retry attempts
    /// </summary>
    [DataMember(Order = 3, Name = "exponent", IsRequired = true), JsonPropertyName("exponent"), YamlMember(Alias = "exponent")]
    public virtual double? Exponent { get; set; }

    /// <summary>
    /// Creates a new constant <see cref="RetryBackoffDuration"/>
    /// </summary>
    /// <param name="period">The constant period of time to wait between retry attempts</param>
    /// <returns>A new constant <see cref="RetryBackoffDuration"/></returns>
    public static RetryBackoffDuration Constant(Duration period) => new() { Type = RetryBackoffDurationType.Constant, Period = period };

    /// <summary>
    /// Creates a new multiplier-based <see cref="RetryBackoffDuration"/>
    /// </summary>
    /// <param name="period">The constant period of time to wait between retry attempts</param>
    /// <returns>A new multiplier-based <see cref="RetryBackoffDuration"/></returns>
    public static RetryBackoffDuration Incremental(Duration period) => new() { Type = RetryBackoffDurationType.Incremental, Period = period };

    /// <summary>
    /// Creates a new exponential <see cref="RetryBackoffDuration"/>
    /// </summary>
    /// <param name="period">The constant period of time to wait between retry attempts</param>
    /// <param name="exponent">The value representing the power to which the specified period of time is to be raised to obtain the time to wait between each retry attempts</param>
    /// <returns>A new exponential <see cref="RetryBackoffDuration"/></returns>
    public static RetryBackoffDuration Exponential(Duration period, double exponent) => new() { Type = RetryBackoffDurationType.Exponential, Period = period, Exponent = exponent };

}