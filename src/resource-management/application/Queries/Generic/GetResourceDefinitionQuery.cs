using CloudStreams.Core.Data.Models;
using CloudStreams.Core.Infrastructure.Services;

namespace CloudStreams.ResourceManagement.Application.Queries.Generic;

/// <summary>
/// Represents the <see cref="IQuery{TResult}"/> used to get the definition of the specified <see cref="IResource"/> type
/// </summary>
/// <typeparam name="TResource">The type of <see cref="IResource"/> to get the definition of</typeparam>
public class GetResourceDefinitionQuery<TResource>
    : IQuery<IResourceDefinition>
    where TResource : class, IResource, new()
{



}

/// <summary>
/// Represents the service used to handle <see cref="GetResourceDefinitionQuery{TResource}"/> instances
/// </summary>
/// <typeparam name="TResource">The type of <see cref="IResource"/> to get</typeparam>
public class GetResourceDefinitionQueryHandler<TResource>
    : IQueryHandler<GetResourceDefinitionQuery<TResource>, IResourceDefinition>
    where TResource : class, IResource, new()
{

    IResourceRepository _ResourceRepository;

    /// <inheritdoc/>
    public GetResourceDefinitionQueryHandler(IResourceRepository resourceRepository)
    {
        this._ResourceRepository = resourceRepository;
    }

    async Task<Response<IResourceDefinition>> MediatR.IRequestHandler<GetResourceDefinitionQuery<TResource>, Response<IResourceDefinition>>.Handle(GetResourceDefinitionQuery<TResource> query, CancellationToken cancellationToken)
    {
        var definition = await this._ResourceRepository.GetResourceDefinitionAsync<TResource>(cancellationToken).ConfigureAwait(false);
        if (definition == null) return Response.ResourceDefinitionNotFound<TResource, IResourceDefinition>();
        return this.Ok(definition);
    }

}
