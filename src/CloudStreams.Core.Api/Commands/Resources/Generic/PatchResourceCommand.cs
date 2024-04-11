using CloudStreams.Core.Api.Commands.Resources;
using Neuroglia;
using Neuroglia.Data;
using Neuroglia.Data.Infrastructure.ResourceOriented;
using Neuroglia.Data.Infrastructure.ResourceOriented.Services;
using Neuroglia.Mediation;

namespace CloudStreams.Core.Api.Commands.Resources.Generic;

/// <summary>
/// Represents the <see cref="ICommand"/> used to delete an existing <see cref="IResource"/>
/// </summary>
/// <typeparam name="TResource">The type of <see cref="IResource"/> to patch</typeparam>
public class PatchResourceCommand<TResource>
    : Command<TResource>
    where TResource : class, IResource, new()
{

    /// <summary>
    /// Initializes a new <see cref="PatchResourceCommand{TResource}"/>
    /// </summary>
    /// <param name="name">The name of the <see cref="IResource"/> to patch</param>
    /// <param name="namespace">The namespace the <see cref="IResource"/> to patch belongs to</param>
    /// <param name="patch">The patch to apply</param>
    /// <param name="dryRun">A boolean indicating whether or not to persist changes</param>
    public PatchResourceCommand(string name, string? @namespace, Patch patch, bool dryRun)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));
        Name = name;
        Namespace = @namespace;
        Patch = patch ?? throw new ArgumentNullException(nameof(patch));
        DryRun = dryRun;
    }

    /// <summary>
    /// Gets the name of the <see cref="IResource"/> to patch
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the name of the <see cref="IResource"/> to patch
    /// </summary>
    public string? Namespace { get; }

    /// <summary>
    /// Gets the patch to apply
    /// </summary>
    public Patch Patch { get; }

    /// <summary>
    /// Gets a boolean indicating whether or not to persist changes
    /// </summary>
    public bool DryRun { get; }

}

/// <summary>
/// Represents the service used to handle <see cref="PatchResourceCommand"/>s
/// </summary>
/// <typeparam name="TResource">The type of <see cref="IResource"/> to patch</typeparam>
/// <param name="repository">The service used to manage resources</param>
/// <param name="repository">The service used to manage <see cref="IResource"/>s</param>
public class PatchResourceCommandHandler<TResource>(IRepository repository)
    : ICommandHandler<PatchResourceCommand<TResource>, TResource>
    where TResource : class, IResource, new()
{

    /// <inheritdoc/>
    public virtual async Task<IOperationResult<TResource>> HandleAsync(PatchResourceCommand<TResource> command, CancellationToken cancellationToken)
    {
        var resource = await repository.PatchAsync<TResource>(command.Patch, command.Name, command.Namespace, command.DryRun, cancellationToken).ConfigureAwait(false);
        return this.Ok(resource);
    }

}