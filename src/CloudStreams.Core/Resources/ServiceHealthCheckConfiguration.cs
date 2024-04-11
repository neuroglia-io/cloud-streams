namespace CloudStreams.Core.Resources;

/// <summary>
/// Represents an object used to configure the health checks of a service
/// </summary>
[DataContract]
public record ServiceHealthCheckConfiguration
{

    /// <summary>
    /// Initializes a new <see cref="ServiceHealthCheckConfiguration"/>
    /// </summary>
    public ServiceHealthCheckConfiguration() { }

    /// <summary>
    /// Initializes a new <see cref="ServiceHealthCheckConfiguration"/>
    /// </summary>
    /// <param name="request">An object used to configure the HTTP-based health check request</param>
    /// <param name="interval">The amount of time to wait between every health check request</param>
    public ServiceHealthCheckConfiguration(HttpRequestConfiguration request, Duration? interval = null)
    {
        this.Request = request;
        this.Interval = interval;
    }

    /// <summary>
    /// Gets/sets an object used to configure the HTTP-based health check request
    /// </summary>
    [Required, JsonRequired]
    [DataMember(Order = 1, Name = "request", IsRequired = true), JsonPropertyOrder(1), JsonPropertyName("request"), YamlMember(Order = 1, Alias = "request")]
    public virtual HttpRequestConfiguration Request { get; set; } = null!;

    /// <summary>
    /// Gets/sets the amount of time to wait between every health check request
    /// </summary>
    [DataMember(Order = 2, Name = "interval"), JsonPropertyOrder(2), JsonPropertyName("interval"), YamlMember(Order = 2, Alias = "interval")]
    public virtual Duration? Interval { get; set; }

}
