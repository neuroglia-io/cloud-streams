using CloudStreams.Core.Api;
using CloudStreams.Core.Data.Models;
using CloudStreams.ResourceManagement.Application.Commands.Generic;
using CloudStreams.ResourceManagement.Application.Queries.Generic;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace CloudStreams.ResourceManagement.Api.Controllers;

/// <summary>
/// Represents the <see cref="ApiController"/> used to manage <see cref="Channel"/>s
/// </summary>
[Route("api/resource-management/v1/channels")]
public class ChannelsController
    : ApiController
{

    /// <inheritdoc/>
    public ChannelsController(IMediator mediator)
        : base(mediator)
    {

    }

    /// <summary>
    /// Creates a new channel
    /// </summary>
    /// <param name="resource">The channel to create</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/></returns>
    [HttpPost]
    [ProducesResponseType(typeof(Channel), (int)HttpStatusCode.Created)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest)]
    public async Task<IActionResult> CreateChannel([FromBody] Channel resource, CancellationToken cancellationToken)
    {
        return this.Process(await this.Mediator.Send<Response<Channel>>(new CreateResourceCommand<Channel>(resource), cancellationToken));
    }

    /// <summary>
    /// Gets the channel with the specified name
    /// </summary>
    /// <param name="name">The name of the channel to get</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/></returns>
    [HttpGet("{name}")]
    [ProducesResponseType(typeof(Channel), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    public async Task<IActionResult> GetChannel(string name, CancellationToken cancellationToken)
    {
        return this.Process(await this.Mediator.Send(new GetResourceQuery<Channel>(name), cancellationToken));
    }

    /// <summary>
    /// Lists channels
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/></returns>
    [HttpGet]
    [ProducesResponseType(typeof(Channel), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    public async Task<IActionResult> ListChannels(CancellationToken cancellationToken)
    {
        return this.Process(await this.Mediator.Send(new ListResourceQuery<Channel>(), cancellationToken));
    }

    /// <summary>
    /// Patches the specified channel
    /// </summary>
    /// <param name="patch">The patch to apply</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/></returns>
    [HttpPatch]
    [ProducesResponseType(typeof(Channel), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    public async Task<IActionResult> PatchChannel([FromBody] ResourcePatch<Channel> patch, CancellationToken cancellationToken)
    {
        if (!this.ModelState.IsValid) return this.ValidationProblem(this.ModelState);
        return this.Process(await this.Mediator.Send<Response<Channel>>(new PatchResourceCommand<Channel>(patch), cancellationToken));
    }

    /// <summary>
    /// Updates the specified channel
    /// </summary>
    /// <param name="resource">The channel to update</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    [HttpPut]
    [ProducesResponseType(typeof(Channel), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    public async Task<IActionResult> PutChannel([FromBody] Channel resource, CancellationToken cancellationToken)
    {
        if (!this.ModelState.IsValid) return this.ValidationProblem(this.ModelState);
        return this.Process(await this.Mediator.Send<Response<Channel>>(new PutResourceCommand<Channel>(resource), cancellationToken));
    }

    /// <summary>
    /// Deletes the specified channel
    /// </summary>
    /// <param name="name">The name of the channel to delete</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/></returns>
    [HttpDelete("{name}")]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    public async Task<IActionResult> DeleteChannel(string name, CancellationToken cancellationToken)
    {
        return this.Process(await this.Mediator.Send(new DeleteResourceCommand<Channel>(name), cancellationToken));
    }

}