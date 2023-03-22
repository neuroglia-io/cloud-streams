using CloudStreams.Core.Data.Models;
using CloudStreams.Core.Infrastructure.Services;

namespace CloudStreams.ResourceManagement.Application.Commands.Generic;

/// <summary>
/// Represents the <see cref="ICommand"/> used to delete an existsing <see cref="IResource"/>
/// </summary>
/// <typeparam name="TResource">The type of <see cref="IResource"/> to delete</typeparam>
public class DeleteResourceCommand<TResource>
    : ICommand
{

    /// <summary>
    /// Initializes a new <see cref="DeleteResourceCommand{TResource}"/>
    /// </summary>
    /// <param name="name">The name of the <see cref="IResource"/> to delete</param>
    /// <param name="namespace">The namespace of the <see cref="IResource"/> to delete</param>
    public DeleteResourceCommand(string name, string? @namespace = null)
    {
        this.Name = name;
        this.Namespace = @namespace;
    }

    /// <summary>
    /// Gets the name of the <see cref="IResource"/> to delete
    /// </summary>
    public virtual string Name { get; }

    /// <summary>
    /// Gets the namespace of the <see cref="IResource"/> to delete
    /// </summary>
    public virtual string? Namespace { get; }

}

/// <summary>
/// Represents the service used to handle <see cref="DeleteResourceCommandHandler{TResource}"/>s
/// </summary>
/// <typeparam name="TResource">The type of <see cref="IResource"/> to put</typeparam>
public class DeleteResourceCommandHandler<TResource>
    : ICommandHandler<DeleteResourceCommand<TResource>>
    where TResource : class, IResource, new()
{

    readonly IResourceRepository _ResourceRepository;

    /// <inheritdoc/>
    public DeleteResourceCommandHandler(IResourceRepository resourceRepository)
    {
        this._ResourceRepository = resourceRepository;
    }

    async Task<Response> MediatR.IRequestHandler<DeleteResourceCommand<TResource>, Response>.Handle(DeleteResourceCommand<TResource> command, CancellationToken cancellationToken)
    {
        await this._ResourceRepository.DeleteResourceAsync<TResource>(command.Name, command.Namespace, cancellationToken).ConfigureAwait(false);
        return this.Ok();
    }

}