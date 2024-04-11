namespace CloudStreams.Core.Resources;

/// <summary>
/// Represents an object used to configure the way a broker should dispatch cloud events
/// </summary>
[DataContract]
public record BrokerDispatchConfiguration
{

    /// <summary>
    /// Initializes a new <see cref="BrokerDispatchConfiguration"/>
    /// </summary>
    public BrokerDispatchConfiguration() { }

    /// <summary>
    /// Initializes a new <see cref="BrokerDispatchConfiguration"/>
    /// </summary>
    /// <param name="retryPolicy">The retry policy that applies by default to all subscriptions managed by the broker</param>
    /// <param name="sequencing">An object used to configure the way the configured broker should sequence cloud events dispatched to consumers</param>
    public BrokerDispatchConfiguration(HttpClientRetryPolicy? retryPolicy = null, CloudEventSequencingConfiguration? sequencing = null)
    {
        this.RetryPolicy = retryPolicy;
        this.Sequencing = sequencing;
    }

    /// <summary>
    /// Gets/sets the retry policy that applies by default to all subscriptions managed by the broker
    /// </summary>
    [DataMember(Order = 1, Name = "retryPolicy"), JsonPropertyOrder(1), JsonPropertyName("retryPolicy"), YamlMember(Order = 1, Alias = "retryPolicy")]
    public virtual HttpClientRetryPolicy? RetryPolicy { get; set; }

    /// <summary>
    /// Gets/sets an object used to configure the way the configured broker should sequence cloud events dispatched to consumers
    /// </summary>
    [DataMember(Order = 2, Name = "sequencing"), JsonPropertyOrder(2), JsonPropertyName("sequencing"), YamlMember(Order = 2, Alias = "sequencing")]
    public virtual CloudEventSequencingConfiguration? Sequencing { get; set; }

}
