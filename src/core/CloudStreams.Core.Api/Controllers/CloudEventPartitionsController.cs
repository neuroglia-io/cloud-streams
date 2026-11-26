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
    /// Lists, page by page, the ids of the cloud event partitions of the specified type, sorted alphabetically
    /// </summary>
    /// <param name="type">The type of the partitions to get the id of</param>
    /// <param name="prefix">The prefix, matched case-insensitively, the ids to list must start with, if any</param>
    /// <param name="maxResults">The maximum amount of ids to return, between 1 and 1000. Defaults to 100</param>
    /// <param name="continuationToken">The token returned with the previous page, used to get the next one, if any</param>
    /// <param name="namespace">The namespace the partitions belong to. Defaults to the default namespace</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/></returns>
    [HttpGet("{type}")]
    [ProducesResponseType(typeof(PartitionIdCollection), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(Neuroglia.ProblemDetails), (int)HttpStatusCode.BadRequest)]
    public virtual async Task<IActionResult> ListPartitionsByType(CloudEventPartitionType type, string? prefix = null, int? maxResults = null, string? continuationToken = null, [FromQuery(Name = "namespace")] string? @namespace = null, CancellationToken cancellationToken = default)
    {
        if (!this.ModelState.IsValid) return this.ValidationProblem(this.ModelState);
        return this.Process(await this.Mediator.ExecuteAsync(new ListEventPartitionIdsQuery(type, prefix, maxResults, continuationToken, @namespace), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Lists, page by page, the cloud event partitions of the specified type with their metadata (length, first and last event), sorted like their ids
    /// </summary>
    /// <param name="type">The type of the partitions to list</param>
    /// <param name="prefix">The prefix, matched case-insensitively, the ids of the partitions to list must start with, if any</param>
    /// <param name="maxResults">The maximum amount of partitions to return, between 1 and 100. Defaults to 25</param>
    /// <param name="continuationToken">The token returned with the previous page, used to get the next one, if any</param>
    /// <param name="namespace">The namespace the partitions belong to. Defaults to the default namespace</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/></returns>
    /// <remarks>See #226</remarks>
    [HttpGet("{type}/metadata")]
    [ProducesResponseType(typeof(PartitionMetadataCollection), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(Neuroglia.ProblemDetails), (int)HttpStatusCode.BadRequest)]
    public virtual async Task<IActionResult> ListPartitionMetadataByType(CloudEventPartitionType type, string? prefix = null, int? maxResults = null, string? continuationToken = null, [FromQuery(Name = "namespace")] string? @namespace = null, CancellationToken cancellationToken = default)
    {
        if (!this.ModelState.IsValid) return this.ValidationProblem(this.ModelState);
        return this.Process(await this.Mediator.ExecuteAsync(new ListEventPartitionsQuery(type, prefix, maxResults, continuationToken, @namespace), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Gets the specified cloud event partition's metadata
    /// </summary>
    /// <param name="type">The type of the partition to get the metadata of</param>
    /// <param name="id">The id of the partition to get the metadata of</param>
    /// <param name="namespace">The namespace the partitions belong to. Defaults to the default namespace</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/></returns>
    [HttpGet("{type}/byId")]
    [ProducesResponseType(typeof(PartitionMetadata), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(Neuroglia.ProblemDetails), (int)HttpStatusCode.BadRequest)]
    public virtual async Task<IActionResult> GetPartitionMetadata(CloudEventPartitionType type, [FromQuery] string id, [FromQuery(Name = "namespace")] string? @namespace = null, CancellationToken cancellationToken = default)
    {
        if (!this.ModelState.IsValid) return this.ValidationProblem(this.ModelState);
        return this.Process(await this.Mediator.ExecuteAsync(new GetEventPartitionMetadataQuery(new(type, id), @namespace), cancellationToken).ConfigureAwait(false));
    }

}