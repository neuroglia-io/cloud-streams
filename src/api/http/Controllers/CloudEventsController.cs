using CloudStreams.Api.Commands.CloudEvents;

namespace CloudStreams.Api.Http.Controllers;

/// <summary>
/// Represents the API controller used to manage events
/// </summary>
[Route("api/v1/cloud-events")]
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
    [ProducesResponseType((int)HttpStatusCode.ServiceUnavailable)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest)]
    [ProducesResponseType((int)HttpStatusCode.Forbidden)]
    public virtual async Task<IActionResult> PublishCloudEvent([FromBody] CloudEvent e, CancellationToken cancellationToken)
    {
        if (!this.ModelState.IsValid) return this.ValidationProblem(this.ModelState);
        return this.Process(await this.Mediator.Send(new ConsumeEventCommand(e), cancellationToken).ConfigureAwait(false));
    }

}
