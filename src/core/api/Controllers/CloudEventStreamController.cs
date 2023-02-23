using CloudStreams.Core.Application.Queries.Streams;

namespace CloudStreams.Core.Api.Controllers;

/// <summary>
/// Represents the API controller used to manage the cloud event streams
/// </summary>
[Route("api/core/v1/cloud-events/stream")]
public class CloudEventStreamController
    : ApiController
{

    /// <inheritdoc/>
    public CloudEventStreamController(IMediator mediator) : base(mediator) { }

    /// <summary>
    /// Gets the cloud event stream's metadata
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/></returns>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<CloudEventStreamMetadata>), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest)]
    public virtual async Task<IActionResult> GetStreamMetadata(CancellationToken cancellationToken)
    {
        if (!this.ModelState.IsValid) return this.ValidationProblem(this.ModelState);
        return this.Process(await this.Mediator.Send(new GetEventStreamMetadataQuery(), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Reads the cloud event stream
    /// </summary>
    /// <param name="options">The object used to configure the query to perform</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/></returns>
    [HttpGet("read")]
    [ProducesResponseType(typeof(IEnumerable<CloudEvent>), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest)]
    public virtual async Task<IActionResult> ReadStream([FromQuery] CloudEventStreamReadOptions options, CancellationToken cancellationToken)
    {
        if (!this.ModelState.IsValid) return this.ValidationProblem(this.ModelState);
        return this.Process(await this.Mediator.Send(new ReadEventStreamQuery(options), cancellationToken).ConfigureAwait(false));
    }

}
