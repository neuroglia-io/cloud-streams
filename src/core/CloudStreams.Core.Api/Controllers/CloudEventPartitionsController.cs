// Copyright © 2024-Present The Cloud Streams Authors
//
// Licensed under the Apache License, Version 2.0 (the "License"),
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using CloudStreams.Core.Application.Queries.Partitions;

namespace CloudStreams.Core.Api.Controllers;

/// <summary>
/// Represents the API controller used to manage the cloud event partitions
/// </summary>
/// <inheritdoc/>
[Route("api/core/v1/cloud-events/partitions")]
public class CloudEventPartitionsController(IMediator mediator)
    : ApiController(mediator)
{

    /// <summary>
    /// Lists the ids of the cloud event partitions of the specified type
    /// </summary>
    /// <param name="type">The type of the partitions to get the id of</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/></returns>
    [HttpGet("{type}")]
    [ProducesResponseType(typeof(IEnumerable<string>), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(Neuroglia.ProblemDetails), (int)HttpStatusCode.BadRequest)]
    public virtual async Task<IActionResult> ListPartitionsByType(CloudEventPartitionType type, CancellationToken cancellationToken)
    {
        if (!this.ModelState.IsValid) return this.ValidationProblem(this.ModelState);
        return this.Process(await this.Mediator.ExecuteAsync(new ListEventPartitionIdsQuery(type), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Gets the specified cloud event partition's metadata
    /// </summary>
    /// <param name="type">The type of the partition to get the metadata of</param>
    /// <param name="id">The id of the partition to get the metadata of</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/></returns>
    [HttpGet("{type}/byId")]
    [ProducesResponseType(typeof(PartitionMetadata), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(Neuroglia.ProblemDetails), (int)HttpStatusCode.BadRequest)]
    public virtual async Task<IActionResult> GetPartitionMetadata(CloudEventPartitionType type, [FromQuery]string id, CancellationToken cancellationToken)
    {
        if (!this.ModelState.IsValid) return this.ValidationProblem(this.ModelState);
        return this.Process(await this.Mediator.ExecuteAsync(new GetEventPartitionMetadataQuery(new(type, id)), cancellationToken).ConfigureAwait(false));
    }

}