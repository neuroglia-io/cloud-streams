namespace CloudStreams.Infrastructure.Services;

/// <summary>
/// Defines the fundamentals of a service used to manage authorization
/// </summary>
public interface IAuthorizationManager
{

    /// <summary>
    /// Evaluates a <see cref="CloudEvent"/> against the specified policy
    /// </summary>
    /// <param name="e">The <see cref="CloudEvent"/> to evaluate</param>
    /// <param name="policy">The <see cref="CloudEventAuthorizationPolicy"/> to evaluate the event against</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="Response"/>, used to describe the result of the evaluation</returns>
    Task<Response> EvaluateAsync(CloudEvent e, CloudEventAuthorizationPolicy policy, CancellationToken cancellationToken = default);

}
