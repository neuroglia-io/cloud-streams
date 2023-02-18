using CloudStreams.Gateway.Application.Queries.Partitions;

namespace CloudStreams.Gateway.Api.Controllers;

/// <summary>
/// Represents the API controller used to manage the cloud event partitions
/// </summary>
[Route("api/v1/cloud-events/partitions")]
public class CloudEventPartitionsController
    : ApiController
{

    /// <inheritdoc/>
    public CloudEventPartitionsController(IMediator mediator) : base(mediator) { }

    /// <summary>
    /// Lists the metadata of the cloud event partitions of the specified type
    /// </summary>
    /// <param name="type">The type of the partitions to get the metadata of</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/></returns>
    [HttpGet("{type}")]
    [ProducesResponseType(typeof(IEnumerable<CloudEventStreamMetadata>), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest)]
    public virtual async Task<IActionResult> ListPartitionsByType(CloudEventPartitionType type, CancellationToken cancellationToken)
    {
        if (!this.ModelState.IsValid) return this.ValidationProblem(this.ModelState);
        return this.Process(await this.Mediator.Send(new ListEventPartitionsMetadataQuery(type), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Gets the specificied cloud event partition's metadata
    /// </summary>
    /// <param name="type">The type of the partition to get the metadata of</param>
    /// <param name="id">The id of the partition to get the metadata of</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/></returns>
    [HttpGet("{type}/{id}")]
    [ProducesResponseType(typeof(IEnumerable<CloudEventStreamMetadata>), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest)]
    public virtual async Task<IActionResult> GetPartitionMetadata(CloudEventPartitionType type, string id, CancellationToken cancellationToken)
    {
        if (!this.ModelState.IsValid) return this.ValidationProblem(this.ModelState);
        return this.Process(await this.Mediator.Send(new GetEventPartitionMetadataQuery(new(type, id)), cancellationToken).ConfigureAwait(false));
    }

}