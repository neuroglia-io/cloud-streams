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
using CloudStreams.Core.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Neuroglia.Eventing.CloudEvents;

namespace CloudStreams.Core.Api.Controllers;

/// <summary>
/// Represents the API controller used to search and publish cloud events on behalf of management API users (see #222 and #223)
/// </summary>
/// <param name="mediator">The service used to mediate calls</param>
/// <param name="serviceProvider">The service used to resolve the cloud event publisher, which only gateways provide</param>
[Route("api/core/v1/cloud-events")]
public class CloudEventsController(IMediator mediator, IServiceProvider serviceProvider)
    : ApiController(mediator)
{

    /// <summary>
    /// Searches the cloud event stream, or a partition
    /// </summary>
    /// <param name="options">The options of the search. Attributes are passed as <c>attributes[name]=pattern</c></param>
    /// <param name="namespace">The namespace whose cloud events to search, or null to search the whole stream, which requires access to the whole cluster</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/> whose body is the records found, and the offset to continue the search from</returns>
    [HttpGet("search")]
    [ProducesResponseType(typeof(CloudEventSearchResult), (int)HttpStatusCode.OK)]
    [ProducesErrorResponseType(typeof(Neuroglia.ProblemDetails))]
    public virtual async Task<IActionResult> Search([FromQuery] CloudEventSearchOptions options, [FromQuery(Name = "namespace")] string? @namespace = null, CancellationToken cancellationToken = default)
    {
        var result = await this.Mediator.ExecuteAsync(new SearchCloudEventsQuery(options, @namespace), cancellationToken).ConfigureAwait(false);
        return result.Status is < 200 or >= 300 ? this.Problem(result) : this.Process(result);
    }

    /// <summary>
    /// Publishes the specified cloud event, as the calling user, through the gateway's admission pipeline, e.g. to test a subscription
    /// </summary>
    /// <param name="cloudEvent">The cloud event to publish</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/> whose body describes what became of the cloud event</returns>
    /// <remarks>Requires the operator role in the namespace of the gateway, and is only available on gateways</remarks>
    [HttpPost("publish")]
    [Authorize(Policy = CloudStreamsAuthenticationDefaults.OperatePolicy)]
    [NamespaceFiltered]
    [ProducesResponseType(typeof(CloudEventPublication), (int)HttpStatusCode.OK)]
    [ProducesErrorResponseType(typeof(Neuroglia.ProblemDetails))]
    public virtual async Task<IActionResult> Publish([FromBody] CloudEvent cloudEvent, CancellationToken cancellationToken = default)
    {
        var publisher = serviceProvider.GetService<ICloudEventPublisher>();
        if (publisher == null) return new ObjectResult(new Neuroglia.ProblemDetails(new Uri("https://cloud-streams.io/problems/not-a-gateway"), "Not Implemented", (int)HttpStatusCode.NotImplemented, "Cloud events can only be published through a gateway"))
        {
            StatusCode = (int)HttpStatusCode.NotImplemented
        };
        var result = await publisher.PublishAsync(cloudEvent, this.User, cancellationToken).ConfigureAwait(false);
        if (result.Status is < 200 or >= 300) return this.Problem(result);
        return this.Ok(new CloudEventPublication()
        {
            Outcome = result.Status switch
            {
                (int)HttpStatusCode.Accepted => CloudStreamsHeaders.Outcomes.Ignored,
                (int)HttpStatusCode.OK => CloudStreamsHeaders.Outcomes.Duplicate,
                _ => CloudStreamsHeaders.Outcomes.Stored
            }
        });
    }

}
