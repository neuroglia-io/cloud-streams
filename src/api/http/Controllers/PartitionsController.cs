using CloudStreams.Api.Queries.Partitions;

namespace CloudStreams.Api.Http.Controllers;

/// <summary>
/// Represents the API controller used to manage the cloud event partitions
/// </summary>
[Route("api/v1/partitions")]
public class PartitionsController
    : ApiController
{

    /// <inheritdoc/>
    public PartitionsController(IMediator mediator) : base(mediator) { }

    /// <summary>
    /// Gets the specificied cloud event partition's metadata
    /// </summary>
    /// <param name="type">The type of the partition to get the metadata of</param>
    /// <param name="id">The id of the partition to get the metadata of</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/></returns>
    [HttpGet("{type}/{id}/metadata")]
    [ProducesResponseType(typeof(IEnumerable<CloudEventStreamMetadata>), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest)]
    public virtual async Task<IActionResult> GetMetadata(CloudEventPartitionType type, string id, CancellationToken cancellationToken)
    {
        if (!this.ModelState.IsValid) return this.ValidationProblem(this.ModelState);
        return this.Process(await this.Mediator.Send(new GetEventPartitionMetadataQuery(new(type, id)), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Lists the metadata of the cloud event partitions of the specified type
    /// </summary>
    /// <param name="type">The type of the partitions to get the metadata of</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/></returns>
    [HttpGet("{type}/metadata")]
    [ProducesResponseType(typeof(IEnumerable<CloudEventStreamMetadata>), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest)]
    public virtual async Task<IActionResult> ListMetadata(CloudEventPartitionType type, CancellationToken cancellationToken)
    {
        if (!this.ModelState.IsValid) return this.ValidationProblem(this.ModelState);
        return this.Process(await this.Mediator.Send(new ListEventPartitionsMetadataQuery(type), cancellationToken).ConfigureAwait(false));
    }

}