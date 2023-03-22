using CloudStreams.Core.Data.Models;
using CloudStreams.Core.Infrastructure.Services;
using Json.Schema.Generation;

namespace CloudStreams.ResourceManagement.Application.Commands.Generic;

/// <summary>
/// Represents the <see cref="ICommand"/> used to put a <see cref="IResource"/>
/// </summary>
public class PutResourceCommand<TResource>
    : ICommand<TResource>
    where TResource : class, IResource, new()
{

    /// <summary>
    /// Initializes a new <see cref="PutResourceCommand{TResource}"/>
    /// </summary>
    /// <param name="resource">The resource to put</param>
    public PutResourceCommand(TResource resource)
    {
        this.Resource = resource;
    }

    /// <summary>
    /// Gets the resource to put
    /// </summary>
    [Required]
    public TResource Resource { get; }

}

/// <summary>
/// Represents the service used to handle <see cref="PutResourceCommand{TResource}"/>s
/// </summary>
/// <typeparam name="TResource">The type of <see cref="IResource"/> to put</typeparam>
public class PutResourceCommandHandler<TResource>
    : ICommandHandler<PutResourceCommand<TResource>, TResource>
    where TResource : class, IResource, new()
{

    readonly IResourceRepository _ResourceRepository;

    /// <inheritdoc/>
    public PutResourceCommandHandler(IResourceRepository resourceRepository)
    {
        this._ResourceRepository = resourceRepository;
    }

    async Task<Response<TResource>> MediatR.IRequestHandler<PutResourceCommand<TResource>, Response<TResource>>.Handle(PutResourceCommand<TResource> command, CancellationToken cancellationToken)
    {
        var resource = await this._ResourceRepository.UpdateResourceAsync(command.Resource, cancellationToken).ConfigureAwait(false);
        return this.Ok(resource);
    }

}
