using CloudStreams.Core.Api.Commands.Resources;
using Neuroglia;
using Neuroglia.Data.Infrastructure.ResourceOriented;
using Neuroglia.Data.Infrastructure.ResourceOriented.Services;
using Neuroglia.Mediation;

namespace CloudStreams.Core.Api.Commands.Resources.Generic;

/// <summary>
/// Represents the <see cref="ICommand"/> used to delete an existing <see cref="IResource"/>
/// </summary>
/// <typeparam name="TResource">The type of <see cref="IResource"/> to create</typeparam>
public class DeleteResourceCommand<TResource>
    : Command<TResource>
    where TResource : class, IResource, new()
{

    /// <summary>
    /// Initializes a new <see cref="DeleteResourceCommand{TResource}"/>
    /// </summary>
    /// <param name="name">The name of the <see cref="IResource"/> to delete</param>
    /// <param name="namespace">The namespace the <see cref="IResource"/> to delete belongs to</param>
    /// <param name="dryRun">A boolean indicating whether or not to persist changes</param>
    public DeleteResourceCommand(string name, string? @namespace, bool dryRun)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));
        Name = name;
        Namespace = @namespace;
        DryRun = dryRun;
    }

    /// <summary>
    /// Gets the name of the <see cref="IResource"/> to delete
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the namespace the <see cref="IResource"/> to delete belongs to
    /// </summary>
    public string? Namespace { get; }

    /// <summary>
    /// Gets a boolean indicating whether or not to persist changes
    /// </summary>
    public bool DryRun { get; }

}

/// <summary>
/// Represents the service used to handle <see cref="DeleteResourceCommand"/>s
/// </summary>
/// <typeparam name="TResource">The type of <see cref="IResource"/> to create</typeparam>
/// <param name="repository">The service used to manage <see cref="IResource"/>s</param>
public class DeleteResourceCommandHandler<TResource>(IRepository repository)
    : ICommandHandler<DeleteResourceCommand<TResource>, TResource>
    where TResource : class, IResource, new()
{

    /// <inheritdoc/>
    public virtual async Task<IOperationResult<TResource>> HandleAsync(DeleteResourceCommand<TResource> command, CancellationToken cancellationToken)
    {
        var resource = await repository.RemoveAsync<TResource>(command.Name, command.Namespace, command.DryRun, cancellationToken).ConfigureAwait(false);
        return this.Ok(resource);
    }

}