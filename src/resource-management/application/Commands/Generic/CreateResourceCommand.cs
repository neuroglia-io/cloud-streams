using CloudStreams.Core.Data.Models;
using CloudStreams.Core.Infrastructure.Services;
using Json.Schema.Generation;

namespace CloudStreams.ResourceManagement.Application.Commands.Generic;

/// <summary>
/// Represents the <see cref="ICommand"/> used to create a new <see cref="IResource"/>
/// </summary>
public class CreateResourceCommand<TResource>
    : ICommand<TResource>
    where TResource : class, IResource, new()
{

    /// <summary>
    /// Initializes a new <see cref="CreateResourceCommand{TResource}"/>
    /// </summary>
    /// <param name="resource">The resource to create</param>
    public CreateResourceCommand(TResource resource)
    {
        this.Resource = resource;
    }

    /// <summary>
    /// Gets the resource to create
    /// </summary>
    [Required]
    public TResource Resource { get; }

}

/// <summary>
/// Represents the service used to handle <see cref="CreateResourceCommand{TResource}"/>s
/// </summary>
/// <typeparam name="TResource">The type of <see cref="IResource"/> to create</typeparam>
public class CreateResourceCommandHandler<TResource>
    : ICommandHandler<CreateResourceCommand<TResource>, TResource>
    where TResource : class, IResource, new()
{

    readonly IResourceRepository _ResourceRepository;

    /// <inheritdoc/>
    public CreateResourceCommandHandler(IResourceRepository resourceRepository)
    {
        this._ResourceRepository = resourceRepository;
    }

    async Task<Response<TResource>> MediatR.IRequestHandler<CreateResourceCommand<TResource>, Response<TResource>>.Handle(CreateResourceCommand<TResource> command, CancellationToken cancellationToken)
    {
        var resource = await this._ResourceRepository.AddResourceAsync(command.Resource, cancellationToken).ConfigureAwait(false);
        return this.Ok(resource);
    }

}
