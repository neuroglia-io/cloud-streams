namespace CloudStreams.Core.Resources;

/// <summary>
/// Represents an object used to configure a retry policy
/// </summary>
[DataContract]
public record RetryPolicy
{

    /// <summary>
    /// Initializes a new <see cref="RetryPolicy"/>
    /// </summary>
    public RetryPolicy() { }

    /// <summary>
    /// Initializes a new <see cref="RetryPolicy"/>
    /// </summary>
    /// <param name="backoffDuration"></param>
    /// <param name="maxAttempts"></param>
    public RetryPolicy(RetryBackoffDuration backoffDuration, int? maxAttempts = null)
    {
        this.BackoffDuration = backoffDuration ?? throw new ArgumentNullException(nameof(backoffDuration));
        this.MaxAttempts = maxAttempts;
    }

    /// <summary>
    /// Gets/sets an object used to configure the backoff duration between retry attempts
    /// </summary>
    [Required]
    [DataMember(Order = 1, Name = "backoffDuration", IsRequired = true), JsonPropertyName("backoffDuration"), YamlMember(Alias = "backoffDuration")]
    public virtual RetryBackoffDuration BackoffDuration { get; set; } = null!;

    /// <summary>
    /// Gets/sets the maximum retry attempts to perform. If not set, it will retry forever
    /// </summary>
    [DataMember(Order = 2, Name = "maxAttempts"), JsonPropertyName("maxAttempts"), YamlMember(Alias = "maxAttempts")]
    public virtual int? MaxAttempts { get; set; }

}
