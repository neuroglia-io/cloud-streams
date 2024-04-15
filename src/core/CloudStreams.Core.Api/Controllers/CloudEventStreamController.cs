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

using CloudStreams.Core.Application.Queries.Streams;

namespace CloudStreams.Core.Api.Controllers;

/// <summary>
/// Represents the API controller used to manage the cloud event streams
/// </summary>
/// <inheritdoc/>
[Route("api/core/v1/cloud-events/stream")]
public class CloudEventStreamController(IMediator mediator)
    : ApiController(mediator)
{

    /// <summary>
    /// Gets the cloud event stream's metadata
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/></returns>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<StreamMetadata>), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(Neuroglia.ProblemDetails), (int)HttpStatusCode.BadRequest)]
    public virtual async Task<IActionResult> GetStreamMetadata(CancellationToken cancellationToken)
    {
        if (!this.ModelState.IsValid) return this.ValidationProblem(this.ModelState);
        return this.Process(await this.Mediator.ExecuteAsync(new GetEventStreamMetadataQuery(), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Reads the cloud event stream
    /// </summary>
    /// <param name="options">The object used to configure the query to perform</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/></returns>
    [HttpGet("read")]
    [ProducesResponseType(typeof(IEnumerable<object>), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(Neuroglia.ProblemDetails), (int)HttpStatusCode.BadRequest)]
    public virtual async Task<IActionResult> ReadStream([FromQuery] StreamReadOptions options, CancellationToken cancellationToken)
    {
        if (!this.ModelState.IsValid) return this.ValidationProblem(this.ModelState);
        return this.Process(await this.Mediator.ExecuteAsync(new ReadEventStreamQuery(options), cancellationToken).ConfigureAwait(false));
    }

}
