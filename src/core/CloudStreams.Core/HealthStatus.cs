namespace CloudStreams.Core;

/// <summary>
/// Enumerates default health statuses
/// </summary>
public static class HealthStatus
{

    /// <summary>
    /// Indicates that the service is healthy
    /// </summary>
    public const string Healthy = "healthy";

    /// <summary>
    /// Indicates that the service is unhealthy
    /// </summary>
    public const string Unhealthy = "unhealthy";

    /// <summary>
    /// Indicates that the service is degraded
    /// </summary>
    public const string Degraded = "degraded";

}