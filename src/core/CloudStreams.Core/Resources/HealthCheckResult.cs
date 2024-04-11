namespace CloudStreams.Core.Resources;

/// <summary>
/// Represents an object used to describe the result of an health check
/// </summary>
[DataContract]
public record HealthCheckResult
{

    /// <summary>
    /// Initializes a new <see cref="HealthCheckResult"/>
    /// </summary>
    public HealthCheckResult() { }

    /// <summary>
    /// Initializes a new <see cref="HealthCheckResult"/>
    /// </summary>
    /// <param name="name">The name of the described check</param>
    /// <param name="status">The status of the described check. Supported values are 'healthy', 'unhealthy' or 'degraded'</param>
    /// <param name="duration">The duration of the described check</param>
    /// <param name="data">A key/value mapping of the check's data</param>
    public HealthCheckResult(string name, string status, TimeSpan? duration = null, IDictionary<string, object>? data = null)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));
        if (string.IsNullOrWhiteSpace(status)) throw new ArgumentNullException(nameof(status));
        this.Name = name;
        this.Status = status;
        this.Duration = duration;
        this.Data = data;
    }

    /// <summary>
    /// Gets/sets the name of the described check
    /// </summary>
    [Required, JsonRequired, MinLength(3)]
    [DataMember(Order = 1, Name = "name", IsRequired = true), JsonPropertyOrder(1), JsonPropertyName("name"), YamlMember(Order = 1, Alias = "name")]
    public virtual string Name { get; set; } = null!;

    /// <summary>
    /// Gets/sets the status of the described check. Supported values are 'healthy', 'unhealthy' or 'degraded'
    /// </summary>
    [Required, JsonRequired, MinLength(3)]
    [DataMember(Order = 2, Name = "status", IsRequired = true), JsonPropertyOrder(2), JsonPropertyName("status"), YamlMember(Order = 2, Alias = "status")]
    public virtual string Status { get; set; } = null!;

    /// <summary>
    /// Gets/sets the duration of the described check
    /// </summary>
    [DataMember(Order = 3, Name = "duration", IsRequired = true), JsonPropertyOrder(3), JsonPropertyName("duration"), YamlMember(Order = 3, Alias = "duration")]
    public virtual TimeSpan? Duration { get; set; }

    /// <summary>
    /// Gets/sets a key/value mapping of the check's data
    /// </summary>
    [DataMember(Order = 4, Name = "data", IsRequired = true), JsonPropertyOrder(4), JsonPropertyName("data"), YamlMember(Order = 4, Alias = "data")]
    public virtual IDictionary<string, object>? Data { get; set; }

}