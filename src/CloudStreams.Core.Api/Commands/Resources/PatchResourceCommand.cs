﻿using Neuroglia;
using Neuroglia.Data;
using Neuroglia.Data.Infrastructure.ResourceOriented;
using Neuroglia.Data.Infrastructure.ResourceOriented.Services;
using Neuroglia.Mediation;

namespace CloudStreams.Core.Api.Commands.Resources;

/// <summary>
/// Represents the <see cref="ICommand"/> used to delete an existing <see cref="IResource"/>
/// </summary>
public class PatchResourceCommand
    : Command<IResource>
{

    /// <summary>
    /// Initializes a new <see cref="PatchResourceCommand"/>
    /// </summary>
    protected PatchResourceCommand() { }

    /// <summary>
    /// Initializes a new <see cref="PatchResourceCommand"/>
    /// </summary>
    /// <param name="group">The API group the resource to patch belongs to</param>
    /// <param name="version">The version of the resource to patch</param>
    /// <param name="plural">The plural name of the type of resource to patch</param>
    /// <param name="name">The name of the <see cref="IResource"/> to patch</param>
    /// <param name="namespace">The namespace the <see cref="IResource"/> to patch belongs to</param>
    /// <param name="patch">The patch to apply</param>
    /// <param name="dryRun">A boolean indicating whether or not to persist changes</param>
    public PatchResourceCommand(string group, string version, string plural, string name, string? @namespace, Patch patch, bool dryRun)
    {
        if (string.IsNullOrWhiteSpace(group)) throw new ArgumentNullException(nameof(group));
        if (string.IsNullOrWhiteSpace(version)) throw new ArgumentNullException(nameof(version));
        if (string.IsNullOrWhiteSpace(plural)) throw new ArgumentNullException(nameof(plural));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));
        Group = group;
        Version = version;
        Plural = plural;
        Name = name;
        Namespace = @namespace;
        Patch = patch ?? throw new ArgumentNullException(nameof(patch));
        DryRun = dryRun;
    }

    /// <summary>
    /// Gets the API group the resource to patch belongs to
    /// </summary>
    public string Group { get; } = null!;

    /// <summary>
    /// Gets the version of the resource to patch
    /// </summary>
    public string Version { get; } = null!;

    /// <summary>
    /// Gets the plural name of the type of resource to patch
    /// </summary>
    public string Plural { get; } = null!;

    /// <summary>
    /// Gets the name of the <see cref="IResource"/> to patch
    /// </summary>
    public string Name { get; } = null!;

    /// <summary>
    /// Gets the name of the <see cref="IResource"/> to patch
    /// </summary>
    public string? Namespace { get; }

    /// <summary>
    /// Gets the patch to apply
    /// </summary>
    public Patch Patch { get; } = null!;

    /// <summary>
    /// Gets a boolean indicating whether or not to persist changes
    /// </summary>
    public bool DryRun { get; }

}

/// <summary>
/// Represents the service used to handle <see cref="PatchResourceCommand"/>s
/// </summary>
/// <param name="repository">The service used to manage <see cref="IResource"/>s</param>
public class PatchResourceCommandHandler(IRepository repository)
    : ICommandHandler<PatchResourceCommand, IResource>
{

    /// <inheritdoc/>
    public virtual async Task<IOperationResult<IResource>> HandleAsync(PatchResourceCommand command, CancellationToken cancellationToken)
    {
        var resource = await repository.PatchAsync(command.Patch, command.Group, command.Version, command.Plural, command.Name, command.Namespace, command.DryRun, cancellationToken).ConfigureAwait(false);
        return this.Ok(resource);
    }

}