using CloudStreams.Core.Api.Commands.Resources.Generic;
using CloudStreams.Core.Api.Queries.Resources.Generic;
using Microsoft.AspNetCore.Mvc;
using Neuroglia.Data;
using Neuroglia.Data.Infrastructure.ResourceOriented;
using Neuroglia.Mediation;
using Neuroglia.Mediation.AspNetCore;
using System.Net;

namespace CloudStreams.Core.Api;

/// <summary>
/// Represents a <see cref="ResourceApiController{TResource}"/> used to manage cluster resources
/// </summary>
/// <typeparam name="TResource">The type of cluster <see cref="IResource"/> to manage</typeparam>
/// <inheritdoc/>
public abstract class ClusterResourceApiController<TResource>(IMediator mediator)
    : ResourceApiController<TResource>(mediator)
    where TResource : class, IResource, new()
{

    /// <summary>
    /// Gets the specified cluster resourced
    /// </summary>
    /// <param name="name">The name of the resource to get</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/></returns>
    [HttpGet("{name}")]
    [ProducesResponseType(typeof(IAsyncEnumerable<Resource>), (int)HttpStatusCode.OK)]
    [ProducesErrorResponseType(typeof(ProblemDetails))]
    public virtual async Task<IActionResult> GetClusterResource(string name, CancellationToken cancellationToken = default)
    {
        return this.Process(await this.Mediator.ExecuteAsync(new GetResourceQuery<TResource>(name, null), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Patches the specified resource
    /// </summary>
    /// <param name="patch">The patch to apply</param>
    /// <param name="name">The name of the resource to patch</param>
    /// <param name="dryRun">A boolean indicating whether or not to persist changes</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/></returns>
    [HttpPatch("{name}")]
    [ProducesResponseType(typeof(IAsyncEnumerable<Resource>), (int)HttpStatusCode.OK)]
    [ProducesErrorResponseType(typeof(ProblemDetails))]
    public virtual async Task<IActionResult> PatchResource(string name, [FromBody] Patch patch, bool dryRun = false, CancellationToken cancellationToken = default)
    {
        if (!this.ModelState.IsValid) return this.ValidationProblem(this.ModelState);
        return this.Process(await this.Mediator.ExecuteAsync(new PatchResourceCommand<TResource>(name, null, patch, dryRun), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Deletes the specified resource
    /// </summary>
    /// <param name="name">The name of the resource to delete</param>
    /// <param name="dryRun">A boolean indicating whether or not to persist changes</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/></returns>
    [HttpDelete("{name}")]
    [ProducesResponseType(typeof(IAsyncEnumerable<Resource>), (int)HttpStatusCode.OK)]
    [ProducesErrorResponseType(typeof(ProblemDetails))]
    public virtual async Task<IActionResult> DeleteResource(string name, bool dryRun = false, CancellationToken cancellationToken = default)
    {
        if (!this.ModelState.IsValid) return this.ValidationProblem(this.ModelState);
        return this.Process(await this.Mediator.ExecuteAsync(new DeleteResourceCommand<TResource>(name, null, dryRun), cancellationToken).ConfigureAwait(false));
    }

}