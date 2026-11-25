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

using CloudStreams.Core.Application.Queries.Gateways;
using CloudStreams.Core.Application.Services;

namespace CloudStreams.Core.Api.Controllers;

/// <summary>
/// Represents the <see cref="ResourceApiController{TResource}"/> used to manage <see cref="Gateway"/>s
/// </summary>
/// <inheritdoc/>
[Route("api/resources/v1/gateways")]
public class GatewaysController(IMediator mediator)
    : ClusterResourceApiController<Gateway>(mediator)
{

    /// <summary>
    /// Checks the health of the specified gateway service, if any
    /// </summary>
    /// <param name="name">Gets the name of the gateway to check the health of</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/></returns>
    [HttpGet("{name}/health")]
    public virtual async Task<IActionResult> CheckGatewayHealth(string name, CancellationToken cancellationToken = default)
    {
        return this.Process(await this.Mediator.ExecuteAsync(new CheckGatewayHealthQuery(name), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Evaluates a cloud event against the specified spec of the specified gateway, which may not be saved yet, without ingesting it
    /// </summary>
    /// <param name="name">The name of the gateway, which split-horizon policies identify it with</param>
    /// <param name="request">An object that describes the spec to evaluate the cloud event against, and the cloud event</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/> whose body tells what would become of the cloud event, and why, or <c>501 Not Implemented</c> on the standalone API</returns>
    /// <remarks>Requires the admin role, like editing gateways, which the dashboard's gateway form tests events for (see #262)</remarks>
    [HttpPost("{name}/test")]
    [ProducesResponseType(typeof(GatewayAdmissionTestResult), (int)HttpStatusCode.OK)]
    [ProducesErrorResponseType(typeof(Neuroglia.ProblemDetails))]
    public virtual async Task<IActionResult> TestAdmission(string name, [FromBody] GatewayAdmissionTestRequest request, CancellationToken cancellationToken = default)
    {
        if (!this.ModelState.IsValid) return this.ValidationProblem(this.ModelState);
        var tester = this.HttpContext.RequestServices.GetService<IGatewayAdmissionTester>();
        if (tester == null) return new ObjectResult(new Neuroglia.ProblemDetails(new Uri("https://cloud-streams.io/problems/not-a-gateway"), "Not Implemented", (int)HttpStatusCode.NotImplemented, "Cloud events can only be tested by a gateway"))
        {
            StatusCode = (int)HttpStatusCode.NotImplemented
        };
        return this.Ok(await tester.TestAsync(new Gateway(new ResourceMetadata(name, null), request.Spec), request.Event, cancellationToken).ConfigureAwait(false));
    }

}
