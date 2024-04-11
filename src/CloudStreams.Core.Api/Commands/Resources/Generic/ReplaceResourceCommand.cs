using CloudStreams.Core.Api.Commands.Resources;
using Neuroglia;
using Neuroglia.Data.Infrastructure.ResourceOriented;
using Neuroglia.Data.Infrastructure.ResourceOriented.Services;
using Neuroglia.Mediation;
using System.Net;

namespace CloudStreams.Core.Api.Commands.Resources.Generic;

/// <summary>
/// Represents the <see cref="ICommand"/> used to replace an existing <see cref="IResource"/>
/// </summary>
/// <typeparam name="TResource">The type of <see cref="IResource"/> to replace</typeparam>
/// <remarks>
/// Initializes a new <see cref="ReplaceResourceCommand{TResource}"/>
/// </remarks>
/// <param name="resource">The updated <see cref="IResource"/> to replace</param>
/// <param name="dryRun">A boolean indicating whether or not to persist changes</param>
public class ReplaceResourceCommand<TResource>(TResource resource, bool dryRun)
    : Command<TResource>
    where TResource : class, IResource, new()
{

    /// <summary>
    /// Gets the updated <see cref="IResource"/> to replace
    /// </summary>
    public TResource Resource { get; } = resource;

    /// <summary>
    /// Gets a boolean indicating whether or not to persist changes
    /// </summary>
    public bool DryRun { get; } = dryRun;

}


/// <summary>
/// Represents the service used to handle <see cref="ReplaceResourceCommand"/>s
/// </summary>
/// <typeparam name="TResource">The type of <see cref="IResource"/> to replace</typeparam>
/// <param name="repository">The service used to manage <see cref="IResource"/>s</param>
public class ReplaceResourceCommandHandler<TResource>(IRepository repository)
    : ICommandHandler<ReplaceResourceCommand<TResource>, TResource>
    where TResource : class, IResource, new()
{

    /// <inheritdoc/>
    public virtual async Task<IOperationResult<TResource>> HandleAsync(ReplaceResourceCommand<TResource> command, CancellationToken cancellationToken)
    {
        var resource = await repository.ReplaceAsync(command.Resource, command.DryRun, cancellationToken).ConfigureAwait(false);
        return new OperationResult<TResource>((int)HttpStatusCode.OK, resource);
    }

}