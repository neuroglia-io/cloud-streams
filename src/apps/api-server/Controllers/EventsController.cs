using CloudStreams.Application.Commands.CloudEvents;
using CloudStreams.Application.Queries.CloudEvents;

namespace CloudStreams.Api.Server.Controllers;

/// <summary>
/// Represents the API controller used to manage events
/// </summary>
[Route("api/events")]
public class EventsController
    : ApiController
{

    /// <inheritdoc/>
    public EventsController(IMediator mediator) : base(mediator) { }

    /// <summary>
    /// Reads stored cloud events
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/></returns>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<CloudEvent>), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest)]
    public virtual async Task<IActionResult> ReadEvents(CancellationToken cancellationToken)
    {
        return this.Process(await this.Mediator.Send(new ListCloudEventsQuery(), cancellationToken));
    }

    /// <summary>
    /// Publishes the specified cloud event
    /// </summary>
    /// <param name="e">The cloud event to publish</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/></returns>
    [HttpPost("pub")]
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
