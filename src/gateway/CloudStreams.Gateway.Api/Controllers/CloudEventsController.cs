using CloudStreams.Gateway.Application.Commands.CloudEvents;
using Microsoft.AspNetCore.Mvc;
using Neuroglia.Mediation;
using Neuroglia.Mediation.AspNetCore;

namespace CloudStreams.Gateway.Controllers;

/// <summary>
/// Represents the API controller used to manage events
/// </summary>
/// <param name="mediator">The service used to mediate calls</param>
[Route("api/gateway/v1/cloud-events")]
public class CloudEventsController(IMediator mediator)
    : Controller
{

    /// <summary>
    /// Publishes the specified cloud event
    /// </summary>
    /// <param name="e">The cloud event to publish</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/></returns>
    [HttpPost("pub")]
    [Consumes(CloudEventContentType.Json)]
    [ProducesResponseType((int)HttpStatusCode.Accepted)]
    [ProducesResponseType(typeof(Neuroglia.ProblemDetails), (int)HttpStatusCode.ServiceUnavailable)]
    [ProducesResponseType(typeof(Neuroglia.ProblemDetails), (int)HttpStatusCode.BadRequest)]
    [ProducesResponseType(typeof(Neuroglia.ProblemDetails), (int)HttpStatusCode.Forbidden)]
    public virtual async Task<IActionResult> PublishCloudEvent([FromBody] CloudEvent e, CancellationToken cancellationToken)
    {
        if (!this.ModelState.IsValid) return this.ValidationProblem(this.ModelState);
        return this.Process(await mediator.ExecuteAsync(new ConsumeEventCommand(e), cancellationToken).ConfigureAwait(false), (int)HttpStatusCode.Accepted);
    }

}
