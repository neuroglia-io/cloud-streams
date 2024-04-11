namespace CloudStreams.Core.Resources;

/// <summary>
/// Represents an object used to describe an health check's response
/// </summary>
[DataContract]
public record HealthCheckResponse
{

    /// <summary>
    /// Initializes a new <see cref="HealthCheckResponse"/>
    /// </summary>
    public HealthCheckResponse() { }

    /// <summary>
    /// Initializes a new <see cref="HealthCheckResponse"/>
    /// </summary>
    /// <param name="status">The service's status. Supported values are 'healthy', 'unhealthy' or 'degraded'</param>
    /// <param name="checks">A list containing objects that describe the checks that have been performed</param>
    public HealthCheckResponse(string status, IEnumerable<HealthCheckResult>? checks = null)
    {
        if (string.IsNullOrWhiteSpace(status)) throw new ArgumentNullException(nameof(status));
        this.Status = status;
        this.Checks = checks == null ? null : new(checks);
    }

    /// <summary>
    /// Gets/sets the service's status. Supported values are 'healthy', 'unhealthy' or 'degraded'
    /// </summary>
    [Required, JsonRequired, MinLength(3)]
    [DataMember(Order = 1, Name = "status", IsRequired = true), JsonPropertyOrder(1), JsonPropertyName("status"), YamlMember(Order = 1, Alias = "status")]
    public virtual string Status { get; set; } = null!;

    /// <summary>
    /// Gets/sets a list containing objects that describe the checks that have been performed
    /// </summary>
    [DataMember(Order = 2, Name = "checks", IsRequired = true), JsonPropertyOrder(2), JsonPropertyName("checks"), YamlMember(Order = 2, Alias = "checks")]
    public virtual EquatableList<HealthCheckResult>? Checks { get; set; }

}
