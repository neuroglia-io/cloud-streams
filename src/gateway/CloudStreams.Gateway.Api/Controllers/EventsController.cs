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

using CloudStreams.Core;
using CloudStreams.Core.Api;
using CloudStreams.Gateway.Application.Commands.CloudEvents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Neuroglia.Mediation;
using Neuroglia.Mediation.AspNetCore;

namespace CloudStreams.Gateway.Controllers;

/// <summary>
/// Represents the API controller used to manage events
/// </summary>
/// <param name="mediator">The service used to mediate calls</param>
[Route("api/events")]
public class EventsController(IMediator mediator)
    : Controller
{

    /// <summary>
    /// Publishes the specified cloud event
    /// </summary>
    /// <param name="e">The cloud event to publish</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/></returns>
    [HttpPost("pub")]
    [Authorize(Policy = CloudStreamsAuthenticationDefaults.IngestionPolicy)]
    [Consumes(CloudEventContentType.Json)]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    [ProducesResponseType(typeof(Neuroglia.ProblemDetails), (int)HttpStatusCode.ServiceUnavailable)]
    [ProducesResponseType(typeof(Neuroglia.ProblemDetails), (int)HttpStatusCode.BadRequest)]
    [ProducesResponseType(typeof(Neuroglia.ProblemDetails), (int)HttpStatusCode.Forbidden)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    public virtual async Task<IActionResult> PublishCloudEvent([FromBody] CloudEvent e, CancellationToken cancellationToken)
    {
        if (!this.ModelState.IsValid) return this.ValidationProblem(this.ModelState);
        var result = await mediator.ExecuteAsync(new ConsumeEventCommand(e), cancellationToken).ConfigureAwait(false);
        if (result.Status == (int)HttpStatusCode.ServiceUnavailable) this.Response.Headers.RetryAfter = "5";
        if (result.Status is < 200 or >= 300) return this.Process(result);
        // the gateway always answers '204 No Content' on success, so that a subscription bridging two instances never mistakes it for a deferred acknowledgement.
        // Whether the event has been stored or ignored by the split-horizon policy is conveyed by a header (see #179)
        this.Response.Headers[CloudStreamsHeaders.Outcome] = result.Status == (int)HttpStatusCode.Accepted ? CloudStreamsHeaders.Outcomes.Ignored : CloudStreamsHeaders.Outcomes.Stored;
        return this.NoContent();
    }

}
