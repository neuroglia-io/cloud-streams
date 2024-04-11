namespace CloudStreams.Core.Resources;

/// <summary>
/// Represents an object used to configure a service
/// </summary>
[DataContract]
public record ServiceConfiguration
{

    /// <summary>
    /// Initializes a new <see cref="ServiceConfiguration"/>
    /// </summary>
    public ServiceConfiguration() { }

    /// <summary>
    /// Initializes a new <see cref="ServiceConfiguration"/>
    /// </summary>
    /// <param name="uri">The base uri of the configured service</param>
    /// <param name="healthChecks">An object used to configure the service's health checks, if any</param>
    public ServiceConfiguration(Uri uri, ServiceHealthCheckConfiguration? healthChecks = null)
    {
        this.Uri = uri ?? throw new ArgumentNullException(nameof(uri));
        this.HealthChecks = healthChecks;
    }

    /// <summary>
    /// Gets/sets the base uri of the configured service
    /// </summary>
    [DataMember(Order = 1, Name = "uri"), JsonPropertyOrder(1), JsonPropertyName("uri"), YamlMember(Order = 1, Alias = "uri")]
    public virtual Uri Uri { get; set; } = null!;

    /// <summary>
    /// Gets/sets an object used to configure the service's health checks, if any
    /// </summary>
    [DataMember(Order = 2, Name = "healthChecks"), JsonPropertyOrder(2), JsonPropertyName("healthChecks"), YamlMember(Order = 2, Alias = "healthChecks")]
    public virtual ServiceHealthCheckConfiguration? HealthChecks { get; set; }

}
