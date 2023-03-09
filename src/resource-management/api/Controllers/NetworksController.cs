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
/// Represents the <see cref="ApiController"/> used to manage <see cref="Network"/>s
/// </summary>
[Route("api/resource-management/v1/networks")]
public class NetworksController
    : ApiController
{

    /// <inheritdoc/>
    public NetworksController(IMediator mediator)
        : base(mediator)
    {

    }

    /// <summary>
    /// Creates a new network
    /// </summary>
    /// <param name="resource">The network to create</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/></returns>
    [HttpPost]
    [ProducesResponseType(typeof(Network), (int)HttpStatusCode.Created)]
    [ProducesResponseType(typeof(Core.Data.Models.ProblemDetails), (int)HttpStatusCode.BadRequest)]
    public async Task<IActionResult> CreateNetwork([FromBody] Network resource, CancellationToken cancellationToken)
    {
        return this.Process(await this.Mediator.Send<Response<Network>>(new CreateResourceCommand<Network>(resource), cancellationToken));
    }

    /// <summary>
    /// Gets the definition of network resources
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/></returns>
    [HttpGet("definition")]
    [ProducesResponseType(typeof(IResourceDefinition), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(Core.Data.Models.ProblemDetails), (int)HttpStatusCode.NotFound)]
    public async Task<IActionResult> GetNetworkDefinition(CancellationToken cancellationToken)
    {
        return this.Process(await this.Mediator.Send(new GetResourceDefinitionQuery<Network>(), cancellationToken));
    }

    /// <summary>
    /// Gets the network with the specified name
    /// </summary>
    /// <param name="name">The name of the network to get</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/></returns>
    [HttpGet("{name}")]
    [ProducesResponseType(typeof(Network), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(Core.Data.Models.ProblemDetails), (int)HttpStatusCode.NotFound)]
    public async Task<IActionResult> GetNetwork(string name, CancellationToken cancellationToken)
    {
        return this.Process(await this.Mediator.Send(new GetResourceQuery<Network>(name), cancellationToken));
    }

    /// <summary>
    /// Lists networks
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/></returns>
    [HttpGet]
    [ProducesResponseType(typeof(Network), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(Core.Data.Models.ProblemDetails), (int)HttpStatusCode.NotFound)]
    public async Task<IActionResult> ListNetworks(CancellationToken cancellationToken)
    {
        return this.Process(await this.Mediator.Send(new ListResourceQuery<Network>(), cancellationToken));
    }

    /// <summary>
    /// Patches the specified network
    /// </summary>
    /// <param name="patch">The patch to apply</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/></returns>
    [HttpPatch]
    [ProducesResponseType(typeof(Network), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(Core.Data.Models.ProblemDetails), (int)HttpStatusCode.BadRequest)]
    [ProducesResponseType(typeof(Core.Data.Models.ProblemDetails), (int)HttpStatusCode.NotFound)]
    public async Task<IActionResult> PatchNetwork([FromBody] ResourcePatch<Network> patch, CancellationToken cancellationToken)
    {
        if (!this.ModelState.IsValid) return this.ValidationProblem(this.ModelState);
        return this.Process(await this.Mediator.Send<Response<Network>>(new PatchResourceCommand<Network>(patch), cancellationToken));
    }

    /// <summary>
    /// Updates the specified network
    /// </summary>
    /// <param name="resource">The network to update</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    [HttpPut]
    [ProducesResponseType(typeof(Network), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(Core.Data.Models.ProblemDetails), (int)HttpStatusCode.BadRequest)]
    [ProducesResponseType(typeof(Core.Data.Models.ProblemDetails), (int)HttpStatusCode.NotFound)]
    public async Task<IActionResult> PutNetwork([FromBody] Network resource, CancellationToken cancellationToken)
    {
        if (!this.ModelState.IsValid) return this.ValidationProblem(this.ModelState);
        return this.Process(await this.Mediator.Send<Response<Network>>(new PutResourceCommand<Network>(resource), cancellationToken));
    }

    /// <summary>
    /// Deletes the specified network
    /// </summary>
    /// <param name="name">The name of the network to delete</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/></returns>
    [HttpDelete("{name}")]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(Core.Data.Models.ProblemDetails), (int)HttpStatusCode.BadRequest)]
    [ProducesResponseType(typeof(Core.Data.Models.ProblemDetails), (int)HttpStatusCode.NotFound)]
    public async Task<IActionResult> DeleteNetwork(string name, CancellationToken cancellationToken)
    {
        return this.Process(await this.Mediator.Send(new DeleteResourceCommand<Network>(name), cancellationToken));
    }

}
