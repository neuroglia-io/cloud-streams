using CloudStreams.Core.Data.Models;
using CloudStreams.Core.Infrastructure.Services;

namespace CloudStreams.ResourceManagement.Application.Queries.Generic;

/// <summary>
/// Represents the <see cref="IQuery{TResult}"/> used to list 
/// </summary>
/// <typeparam name="TResource">The type of <see cref="IResource"/>s to list</typeparam>
public class ListResourceQuery<TResource>
    : IQuery<IAsyncEnumerable<TResource>>
    where TResource : class, IResource, new()
{

    /// <summary>
    /// Initializes a new <see cref="ListResourceQuery{TResource}"/>
    /// </summary>
    /// <param name="namespace">The namespace the resources to list belong to, if any</param>
    /// <param name="labelSelectors">An <see cref="IEnumerable{T}"/> containing the label to select the resources to list by</param>
    public ListResourceQuery(string? @namespace = null, IEnumerable<ResourceLabelSelector>? labelSelectors = null)
    {
        this.Namespace = @namespace;
        this.LabelSelectors = labelSelectors;
    }

    /// <summary>
    /// Gets the namespace the resources to list belong to, if any
    /// </summary>
    public string? Namespace { get; }

    /// <summary>
    /// Gets an <see cref="IEnumerable{T}"/> containing the label to select the resources to list by
    /// </summary>
    public IEnumerable<ResourceLabelSelector>? LabelSelectors { get; }

}

/// <summary>
/// Represents the service used to handle <see cref="ListResourceQuery{TResource}"/> instances
/// </summary>
/// <typeparam name="TResource">The type of <see cref="IResource"/> to list</typeparam>
public class ListResourceQueryHandler<TResource>
    : IQueryHandler<ListResourceQuery<TResource>, IAsyncEnumerable<TResource>>
     where TResource : class, IResource, new()
{

    IResourceRepository _ResourceRepository;

    /// <inheritdoc/>
    public ListResourceQueryHandler(IResourceRepository resourceRepository)
    {
        this._ResourceRepository = resourceRepository;
    }

    async Task<Response<IAsyncEnumerable<TResource>>> MediatR.IRequestHandler<ListResourceQuery<TResource>, Response<IAsyncEnumerable<TResource>>>.Handle(ListResourceQuery<TResource> query, CancellationToken cancellationToken)
    {
        var resources = await this._ResourceRepository.ListResourcesAsync<TResource>(query.Namespace, query.LabelSelectors, cancellationToken).ConfigureAwait(false);
        return this.Ok(resources!);
    }

}