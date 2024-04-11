using CloudStreams.Core;

namespace CloudStreams.Gateway.Services;

/// <summary>
/// Defines the fundamentals of a service that controls <see cref="CloudEvent"/> admission
/// </summary>
public interface ICloudEventAdmissionControl
{

    /// <summary>
    /// Evaluates the admission of the specified <see cref="CloudEvent"/>
    /// </summary>
    /// <param name="e">The <see cref="CloudEvent"/> to evaluate for admission</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="OperationResult{TContent}"/> that describes the result of the evaluation</returns>
    Task<OperationResult<CloudEventDescriptor>> EvaluateAsync(CloudEvent e, CancellationToken cancellationToken = default);

}
