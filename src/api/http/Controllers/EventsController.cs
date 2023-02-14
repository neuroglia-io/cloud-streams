using CloudStreams.Api.Commands.CloudEvents;
using CloudStreams.Api.Queries.CloudEvents;

namespace CloudStreams.Api.Http.Controllers;

/// <summary>
/// Represents the API controller used to manage events
/// </summary>
[Route("api/v1/events")]
public class EventsController
    : ApiController
{

    /// <inheritdoc/>
    public EventsController(IMediator mediator) : base(mediator) { }

    /// <summary>
    /// Reads stored cloud events
    /// </summary>
    /// <param name="options">The object used to configure the query to perform</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/></returns>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<CloudEvent>), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest)]
    public virtual async Task<IActionResult> ReadEvents([FromQuery]CloudEventStreamReadOptions options, CancellationToken cancellationToken)
    {
        if (!this.ModelState.IsValid) return this.ValidationProblem(this.ModelState);
        return this.Process(await this.Mediator.Send(new ListCloudEventsQuery(options), cancellationToken));
    }

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
    public virtual async Task<IActionResult> PublishEvent([FromBody] CloudEvent e, CancellationToken cancellationToken)
    {
        if (!this.ModelState.IsValid) return this.ValidationProblem(this.ModelState);
        return this.Process(await this.Mediator.Send(new ConsumeCloudEventCommand(e), cancellationToken));
    }

}
