using CloudStreams.Core.Api;
using CloudStreams.Gateway.Application.Commands.CloudEvents;

namespace CloudStreams.Gateway.Api.Controllers;

/// <summary>
/// Represents the API controller used to manage events
/// </summary>
[Route("api/gateway/v1/cloud-events")]
public class CloudEventsController
    : ApiController
{

    /// <inheritdoc/>
    public CloudEventsController(IMediator mediator) : base(mediator) { }

    /// <summary>
    /// Publishes the specified cloud event
    /// </summary>
    /// <param name="e">The cloud event to publish</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/></returns>
    [HttpPost("pub")]
    [Consumes(CloudEventMediaTypeNames.CloudEvents, CloudEventMediaTypeNames.CloudEventsJson, CloudEventMediaTypeNames.CloudEventsYaml)]
    [ProducesResponseType((int)HttpStatusCode.Accepted)]
    [ProducesResponseType(typeof(Core.Data.Models.ProblemDetails), (int)HttpStatusCode.ServiceUnavailable)]
    [ProducesResponseType(typeof(Core.Data.Models.ProblemDetails), (int)HttpStatusCode.BadRequest)]
    [ProducesResponseType(typeof(Core.Data.Models.ProblemDetails), (int)HttpStatusCode.Forbidden)]
    public virtual async Task<IActionResult> PublishCloudEvent([FromBody] CloudEvent e, CancellationToken cancellationToken)
    {
        if (!this.ModelState.IsValid) return this.ValidationProblem(this.ModelState);
        return this.Process(await this.Mediator.Send(new ConsumeEventCommand(e), cancellationToken).ConfigureAwait(false));
    }

}
