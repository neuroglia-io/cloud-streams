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

using CloudStreams.Core;
using CloudStreams.Core.Api;
using CloudStreams.Core.Data.Models;
using CloudStreams.ResourceManagement.Application.Commands.Generic;
using CloudStreams.ResourceManagement.Application.Queries.Generic;
using Json.Patch;
using Json.Pointer;
using Json.Schema;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using OpenTelemetry.Resources;
using System.Net;

namespace CloudStreams.ResourceManagement.Api.Controllers;

/// <summary>
/// Represents the <see cref="ApiController"/> used to manage <see cref="Subscription"/>s
/// </summary>
[Route("api/resource-management/v1/subscriptions")]
public class SubscriptionsController
    : ApiController
{

    /// <inheritdoc/>
    public SubscriptionsController(IMediator mediator)
        : base(mediator)
    {

    }

    /// <summary>
    /// Creates a new subscription
    /// </summary>
    /// <param name="resource">The subscription to create</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/></returns>
    [HttpPost]
    [ProducesResponseType(typeof(Subscription), (int)HttpStatusCode.Created)]
    [ProducesResponseType(typeof(Core.Data.Models.ProblemDetails), (int)HttpStatusCode.BadRequest)]
    public async Task<IActionResult> CreateSubscription([FromBody] Subscription resource, CancellationToken cancellationToken)
    {
        return this.Process(await this.Mediator.Send<Response<Subscription>>(new CreateResourceCommand<Subscription>(resource), cancellationToken));
    }

    /// <summary>
    /// Gets the definition of subscription resources
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/></returns>
    [HttpGet("definition")]
    [ProducesResponseType(typeof(IResourceDefinition), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(Core.Data.Models.ProblemDetails), (int)HttpStatusCode.NotFound)]
    public async Task<IActionResult> GetSubscriptionDefinition(CancellationToken cancellationToken)
    {
        return this.Process(await this.Mediator.Send(new GetResourceDefinitionQuery<Subscription>(), cancellationToken));
    }

    /// <summary>
    /// Gets the subscription with the specified name
    /// </summary>
    /// <param name="name">The name of the subscription to get</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/></returns>
    [HttpGet("{name}")]
    [ProducesResponseType(typeof(Subscription), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(Core.Data.Models.ProblemDetails), (int)HttpStatusCode.NotFound)]
    public async Task<IActionResult> GetSubscription(string name, CancellationToken cancellationToken)
    {
        return this.Process(await this.Mediator.Send(new GetResourceQuery<Subscription>(name), cancellationToken));
    }

    /// <summary>
    /// Lists subscriptions
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/></returns>
    [HttpGet]
    [ProducesResponseType(typeof(Subscription), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(Core.Data.Models.ProblemDetails), (int)HttpStatusCode.NotFound)]
    public async Task<IActionResult> ListSubscriptions(CancellationToken cancellationToken)
    {
        return this.Process(await this.Mediator.Send(new ListResourceQuery<Subscription>(), cancellationToken));
    }

    /// <summary>
    /// Patches the specified subscription
    /// </summary>
    /// <param name="patch">The patch to apply</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/></returns>
    [HttpPatch]
    [ProducesResponseType(typeof(Subscription), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest)]
    [ProducesResponseType(typeof(Core.Data.Models.ProblemDetails), (int)HttpStatusCode.NotFound)]
    public async Task<IActionResult> PatchSubscription([FromBody] ResourcePatch<Subscription> patch, CancellationToken cancellationToken)
    {
        if (!this.ModelState.IsValid) return this.ValidationProblem(this.ModelState);
        return this.Process(await this.Mediator.Send<Response<Subscription>>(new PatchResourceCommand<Subscription>(patch), cancellationToken));
    }

    /// <summary>
    /// Patches the status of the specified subscription
    /// </summary>
    /// <param name="patch">The patch to apply</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/></returns>
    [HttpPatch("status")]
    [ProducesResponseType(typeof(Subscription), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest)]
    [ProducesResponseType(typeof(Core.Data.Models.ProblemDetails), (int)HttpStatusCode.NotFound)]
    public async Task<IActionResult> PatchSubscriptionStatus([FromBody] ResourcePatch<Subscription> patch, CancellationToken cancellationToken)
    {
        if (!this.ModelState.IsValid) return this.ValidationProblem(this.ModelState);
        return this.Process(await this.Mediator.Send<Response<Subscription>>(new PatchResourceStatusCommand<Subscription>(patch), cancellationToken));
    }

    /// <summary>
    /// Updates the specified subscription
    /// </summary>
    /// <param name="resource">The subscription to update</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    [HttpPut]
    [ProducesResponseType(typeof(Subscription), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(Core.Data.Models.ProblemDetails), (int)HttpStatusCode.BadRequest)]
    [ProducesResponseType(typeof(Core.Data.Models.ProblemDetails), (int)HttpStatusCode.NotFound)]
    public async Task<IActionResult> PutSubscription([FromBody] Subscription resource, CancellationToken cancellationToken)
    {
        if (!this.ModelState.IsValid) return this.ValidationProblem(this.ModelState);
        return this.Process(await this.Mediator.Send<Response<Subscription>>(new PutResourceCommand<Subscription>(resource), cancellationToken));
    }
   
    /// <summary>
    /// Deletes the specified subscription
    /// </summary>
    /// <param name="name">The name of the subscription to delete</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/></returns>
    [HttpDelete("{name}")]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(Core.Data.Models.ProblemDetails), (int)HttpStatusCode.BadRequest)]
    [ProducesResponseType(typeof(Core.Data.Models.ProblemDetails), (int)HttpStatusCode.NotFound)]
    public async Task<IActionResult> DeleteSubscription(string name, CancellationToken cancellationToken)
    {
        return this.Process(await this.Mediator.Send(new DeleteResourceCommand<Subscription>(name), cancellationToken));
    }

}