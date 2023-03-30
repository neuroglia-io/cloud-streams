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
using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace CloudStreams.ResourceManagement.Api.Controllers;

/// <summary>
/// Represents the <see cref="ApiController"/> used to manage <see cref="Broker"/>s
/// </summary>
[Route("api/resource-management/v1/brokers")]
public class BrokersController
    : ApiController
{

    /// <inheritdoc/>
    public BrokersController(IMediator mediator)
        : base(mediator)
    {

    }

    /// <summary>
    /// Creates a new broker
    /// </summary>
    /// <param name="resource">The broker to create</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/></returns>
    [HttpPost]
    [ProducesResponseType(typeof(Broker), (int)HttpStatusCode.Created)]
    [ProducesResponseType(typeof(Core.Data.Models.ProblemDetails), (int)HttpStatusCode.BadRequest)]
    public async Task<IActionResult> CreateBroker([FromBody] Broker resource, CancellationToken cancellationToken)
    {
        return this.Process(await this.Mediator.Send<Response<Broker>>(new CreateResourceCommand<Broker>(resource), cancellationToken));
    }

    /// <summary>
    /// Gets the definition of broker resources
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/></returns>
    [HttpGet("definition")]
    [ProducesResponseType(typeof(IResourceDefinition), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(Core.Data.Models.ProblemDetails), (int)HttpStatusCode.NotFound)]
    public async Task<IActionResult> GetBrokerDefinition(CancellationToken cancellationToken)
    {
        return this.Process(await this.Mediator.Send(new GetResourceDefinitionQuery<Broker>(), cancellationToken));
    }

    /// <summary>
    /// Gets the broker with the specified name
    /// </summary>
    /// <param name="name">The name of the broker to get</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/></returns>
    [HttpGet("{name}")]
    [ProducesResponseType(typeof(Broker), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(Core.Data.Models.ProblemDetails), (int)HttpStatusCode.NotFound)]
    public async Task<IActionResult> GetBroker(string name, CancellationToken cancellationToken)
    {
        return this.Process(await this.Mediator.Send(new GetResourceQuery<Broker>(name), cancellationToken));
    }

    /// <summary>
    /// Lists brokers
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/></returns>
    [HttpGet]
    [ProducesResponseType(typeof(Broker), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(Core.Data.Models.ProblemDetails), (int)HttpStatusCode.NotFound)]
    public async Task<IActionResult> ListBrokers(CancellationToken cancellationToken)
    {
        return this.Process(await this.Mediator.Send(new ListResourceQuery<Broker>(), cancellationToken));
    }

    /// <summary>
    /// Patches the specified broker
    /// </summary>
    /// <param name="patch">The patch to apply</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/></returns>
    [HttpPatch]
    [ProducesResponseType(typeof(Broker), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest)]
    [ProducesResponseType(typeof(Core.Data.Models.ProblemDetails), (int)HttpStatusCode.NotFound)]
    public async Task<IActionResult> PatchBroker([FromBody] ResourcePatch<Broker> patch, CancellationToken cancellationToken)
    {
        if (!this.ModelState.IsValid) return this.ValidationProblem(this.ModelState);
        return this.Process(await this.Mediator.Send<Response<Broker>>(new PatchResourceCommand<Broker>(patch), cancellationToken));
    }

    /// <summary>
    /// Patches the status of the specified broker
    /// </summary>
    /// <param name="patch">The patch to apply</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/></returns>
    [HttpPatch("status")]
    [ProducesResponseType(typeof(Broker), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest)]
    [ProducesResponseType(typeof(Core.Data.Models.ProblemDetails), (int)HttpStatusCode.NotFound)]
    public async Task<IActionResult> PatchBrokerStatus([FromBody] ResourcePatch<Broker> patch, CancellationToken cancellationToken)
    {
        if (!this.ModelState.IsValid) return this.ValidationProblem(this.ModelState);
        return this.Process(await this.Mediator.Send<Response<Broker>>(new PatchResourceStatusCommand<Broker>(patch), cancellationToken));
    }

    /// <summary>
    /// Updates the specified broker
    /// </summary>
    /// <param name="resource">The broker to update</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    [HttpPut]
    [ProducesResponseType(typeof(Broker), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest)]
    [ProducesResponseType(typeof(Core.Data.Models.ProblemDetails), (int)HttpStatusCode.NotFound)]
    public async Task<IActionResult> PutBroker([FromBody] Broker resource, CancellationToken cancellationToken)
    {
        if (!this.ModelState.IsValid) return this.ValidationProblem(this.ModelState);
        return this.Process(await this.Mediator.Send<Response<Broker>>(new PutResourceCommand<Broker>(resource), cancellationToken));
    }

    /// <summary>
    /// Deletes the specified broker
    /// </summary>
    /// <param name="name">The name of the broker to delete</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/></returns>
    [HttpDelete("{name}")]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(Core.Data.Models.ProblemDetails), (int)HttpStatusCode.BadRequest)]
    [ProducesResponseType(typeof(Core.Data.Models.ProblemDetails), (int)HttpStatusCode.NotFound)]
    public async Task<IActionResult> DeleteBroker(string name, CancellationToken cancellationToken)
    {
        return this.Process(await this.Mediator.Send(new DeleteResourceCommand<Broker>(name), cancellationToken));
    }

}
