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

using CloudStreams.Core.Application.Queries.Gateways;
using CloudStreams.Core.Data;
using Hylo.Api.Http;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CloudStreams.Core.Api.Controllers;

/// <summary>
/// Represents the <see cref="ResourceApiController{TResource}"/> used to manage <see cref="Gateway"/>s
/// </summary>
[Route("api/resources/v1/gateways")]
public class GatewaysController
    : ClusterResourceApiController<Gateway>
{

    /// <inheritdoc/>
    public GatewaysController(IMediator mediator) : base(mediator) { }

    /// <summary>
    /// Checks the health of the specified gateway service, if any
    /// </summary>
    /// <param name="name">Gets the name of the gateway to check the health of</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/></returns>
    [HttpGet("{name}/health")]
    public virtual async Task<IActionResult> CheckGatewayHealth(string name, CancellationToken cancellationToken = default)
    {
        return this.Process(await this.Mediator.Send(new CheckGatewayHealthQuery(name), cancellationToken).ConfigureAwait(false));
    }

}
