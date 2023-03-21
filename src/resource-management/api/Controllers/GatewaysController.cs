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
/// Represents the <see cref="ApiController"/> used to manage <see cref="Gateway"/>s
/// </summary>
[Route("api/resource-management/v1/gateways")]
public class GatewaysController
    : ApiController
{

    /// <inheritdoc/>
    public GatewaysController(IMediator mediator) 
        : base(mediator)
    {

    }

    /// <summary>
    /// Creates a new gateway
    /// </summary>
    /// <param name="resource">The gateway to create</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/></returns>
    [HttpPost]
    [ProducesResponseType(typeof(Gateway), (int)HttpStatusCode.Created)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest)]
    public async Task<IActionResult> CreateGateway([FromBody] Gateway resource, CancellationToken cancellationToken)
    {
        return this.Process(await this.Mediator.Send<Response<Gateway>>(new CreateResourceCommand<Gateway>(resource), cancellationToken));
    }

    /// <summary>
    /// Gets the definition of gateway resources
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/></returns>
    [HttpGet("definition")]
    [ProducesResponseType(typeof(IResourceDefinition), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(Core.Data.Models.ProblemDetails), (int)HttpStatusCode.NotFound)]
    public async Task<IActionResult> GetGatewayDefinition(CancellationToken cancellationToken)
    {
        return this.Process(await this.Mediator.Send(new GetResourceDefinitionQuery<Gateway>(), cancellationToken));
    }

    /// <summary>
    /// Gets the gateway with the specified name
    /// </summary>
    /// <param name="name">The name of the gateway to get</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/></returns>
    [HttpGet("{name}")]
    [ProducesResponseType(typeof(Gateway), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(Core.Data.Models.ProblemDetails), (int)HttpStatusCode.NotFound)]
    public async Task<IActionResult> GetGateway(string name, CancellationToken cancellationToken)
    {
        return this.Process(await this.Mediator.Send(new GetResourceQuery<Gateway>(name), cancellationToken));
    }

    /// <summary>
    /// Lists gateways
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/></returns>
    [HttpGet]
    [ProducesResponseType(typeof(Gateway), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(Core.Data.Models.ProblemDetails), (int)HttpStatusCode.NotFound)]
    public async Task<IActionResult> ListGateways(CancellationToken cancellationToken)
    {
        return this.Process(await this.Mediator.Send(new ListResourceQuery<Gateway>(), cancellationToken));
    }

    /// <summary>
    /// Patches the specified gateway
    /// </summary>
    /// <param name="patch">The patch to apply</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/></returns>
    [HttpPatch]
    [ProducesResponseType(typeof(Gateway), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(Core.Data.Models.ProblemDetails), (int)HttpStatusCode.BadRequest)]
    [ProducesResponseType(typeof(Core.Data.Models.ProblemDetails), (int)HttpStatusCode.NotFound)]
    public async Task<IActionResult> PatchGateway([FromBody] ResourcePatch<Gateway> patch, CancellationToken cancellationToken)
    {
        if (!this.ModelState.IsValid) return this.ValidationProblem(this.ModelState);
        return this.Process(await this.Mediator.Send<Response<Gateway>>(new PatchResourceCommand<Gateway>(patch), cancellationToken));
    }

    /// <summary>
    /// Updates the specified gateway
    /// </summary>
    /// <param name="resource">The gateway to update</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    [HttpPut]
    [ProducesResponseType(typeof(Gateway), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(Core.Data.Models.ProblemDetails), (int)HttpStatusCode.BadRequest)]
    [ProducesResponseType(typeof(Core.Data.Models.ProblemDetails), (int)HttpStatusCode.NotFound)]
    public async Task<IActionResult> PutGateway([FromBody] Gateway resource, CancellationToken cancellationToken)
    {
        if (!this.ModelState.IsValid) return this.ValidationProblem(this.ModelState);
        return this.Process(await this.Mediator.Send<Response<Gateway>>(new PutResourceCommand<Gateway>(resource), cancellationToken));
    }

    /// <summary>
    /// Deletes the specified gateway
    /// </summary>
    /// <param name="name">The name of the gateway to delete</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/></returns>
    [HttpDelete("{name}")]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(Core.Data.Models.ProblemDetails), (int)HttpStatusCode.BadRequest)]
    [ProducesResponseType(typeof(Core.Data.Models.ProblemDetails), (int)HttpStatusCode.NotFound)]
    public async Task<IActionResult> DeleteGateway(string name, CancellationToken cancellationToken)
    {
        return this.Process(await this.Mediator.Send(new DeleteResourceCommand<Gateway>(name), cancellationToken));
    }

}
