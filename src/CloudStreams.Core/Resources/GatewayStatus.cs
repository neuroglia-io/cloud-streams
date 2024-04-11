namespace CloudStreams.Core.Resources;

/// <summary>
/// Represents an object used to describe the status of a gateway
/// </summary>
[DataContract]
public record GatewayStatus
{

    /// <summary>
    /// Gets/sets the gateway's health status
    /// </summary>
    [DataMember(Order = 1, Name = "healthStatus"), JsonPropertyOrder(1), JsonPropertyName("healthStatus"), YamlMember(Order = 1, Alias = "healthStatus")]
    public virtual string? HealthStatus { get; set; }

    /// <summary>
    /// Gets/sets the date and time at which the last gateway health check has been performed
    /// </summary>
    [DataMember(Order = 2, Name = "lastHealthCheckAt"), JsonPropertyOrder(2), JsonPropertyName("lastHealthCheckAt"), YamlMember(Order = 2, Alias = "lastHealthCheckAt")]
    public virtual DateTimeOffset? LastHealthCheckAt { get; set; }

}