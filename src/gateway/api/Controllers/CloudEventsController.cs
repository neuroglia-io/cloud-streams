// Copyright © 2023-Present The Cloud Streams Authors
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

using CloudStreams.Core.Api;
using CloudStreams.Gateway.Application.Commands.CloudEvents;

namespace CloudStreams.Gateway.Api.Controllers;

/// <summary>
/// Represents the API controller used to manage events
/// </summary>
[Route("api/gateway/v1/cloud-events")]
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
    [ProducesResponseType(typeof(Core.Data.Models.ProblemDetails), (int)HttpStatusCode.ServiceUnavailable)]
    [ProducesResponseType(typeof(Core.Data.Models.ProblemDetails), (int)HttpStatusCode.BadRequest)]
    [ProducesResponseType(typeof(Core.Data.Models.ProblemDetails), (int)HttpStatusCode.Forbidden)]
    public virtual async Task<IActionResult> PublishCloudEvent([FromBody] CloudEvent e, CancellationToken cancellationToken)
    {
        if (!this.ModelState.IsValid) return this.ValidationProblem(this.ModelState);
        return this.Process(await this.Mediator.Send(new ConsumeEventCommand(e), cancellationToken).ConfigureAwait(false));
    }

}
