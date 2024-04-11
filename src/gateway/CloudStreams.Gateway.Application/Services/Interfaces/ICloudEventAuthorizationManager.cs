using CloudStreams.Core.Resources;

namespace CloudStreams.Gateway.Application.Services;

/// <summary>
/// Defines the fundamentals of a service used to manage authorization
/// </summary>
public interface ICloudEventAuthorizationManager
{

    /// <summary>
    /// Evaluates a <see cref="CloudEvent"/> against the specified policy
    /// </summary>
    /// <param name="e">The <see cref="CloudEvent"/> to evaluate</param>
    /// <param name="policy">The <see cref="CloudEventAuthorizationPolicy"/> to evaluate the event against</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="OperationResult"/>, used to describe the result of the evaluation</returns>
    Task<OperationResult> EvaluateAsync(CloudEvent e, CloudEventAuthorizationPolicy policy, CancellationToken cancellationToken = default);

}