namespace CloudStreams.Gateway.Application.Services;

/// <summary>
/// Defines the fundamentals of a service used to manage Cloud Streams Gateway related metrics
/// </summary>
public interface IGatewayMetrics
    : IDisposable
{

    /// <summary>
    /// Increments the total count of cloud events rejected after evaluation of defined authorization policies
    /// </summary>
    void IncrementTotalRejectedEvents();

    /// <summary>
    /// Increments the total count of invalid cloud events received by the gateway
    /// </summary>
    void IncrementTotalInvalidEvents();

    /// <summary>
    /// Increments the total count of cloud events ingested by the gateway
    /// </summary>
    void IncrementTotalIngestedEvents();

}
