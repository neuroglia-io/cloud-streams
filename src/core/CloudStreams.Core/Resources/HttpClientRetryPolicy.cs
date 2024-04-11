namespace CloudStreams.Core.Resources;

/// <summary>
/// Represents an object used to configure the retry policy for an http client
/// </summary>
[DataContract]
public record HttpClientRetryPolicy
    : RetryPolicy
{

    /// <summary>
    /// Initializes a new <see cref="HttpClientRetryPolicy"/>
    /// </summary>
    public HttpClientRetryPolicy() { }

    /// <summary>
    /// Initializes a new <see cref="HttpClientRetryPolicy"/>
    /// </summary>
    /// <param name="statusCodes">A list containing the http status codes the retry policy applies to. If not set, the policy will apply to all non-success (200-300) status codes</param>
    /// <param name="circuitBreaker">An object that configures the client's circuit breaker, if any</param>
    public HttpClientRetryPolicy(IEnumerable<int>? statusCodes = null, CircuitBreakerPolicy? circuitBreaker = null)
    {
        this.StatusCodes = statusCodes?.ToList();
        this.CircuitBreaker = circuitBreaker;
    }

    /// <summary>
    /// Gets/sets a list containing the http status codes the retry policy applies to. If not set, the policy will apply to all non-success (200-300) status codes
    /// </summary>
    [DataMember(Order = 1, Name = "statusCodes"), JsonPropertyName("statusCodes"), YamlMember(Alias = "statusCodes")]
    public virtual List<int>? StatusCodes { get; set; }

    /// <summary>
    /// Gets/sets an object that configures the client's circuit breaker, if any
    /// </summary>
    [DataMember(Order = 2, Name = "circuitBreaker"), JsonPropertyName("circuitBreaker"), YamlMember(Alias = "circuitBreaker")]
    public virtual CircuitBreakerPolicy? CircuitBreaker { get; set; }

}
