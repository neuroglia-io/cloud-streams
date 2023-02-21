namespace CloudStreams.Core.Infrastructure.Services;

/// <summary>
/// Defines the fundamentals of a service used to manage the application's resources
/// </summary>
public interface IResourceRepository
{

    /// <summary>
    /// Adds and persist the specified resource to the repository
    /// </summary>
    /// <typeparam name="TResource">The type of resource to add</typeparam>
    /// <param name="resource">The resource to add</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The added resource</returns>
    Task<TResource> AddResourceAsync<TResource>(TResource resource, CancellationToken cancellationToken = default)
        where TResource : class, IResource, new();

    /// <summary>
    /// Gets the specified resource
    /// </summary>
    /// <typeparam name="TResource">The type of resource to get</typeparam>
    /// <param name="name">The name of the resource to get</param>
    /// <param name="namespace">The namespace of the resource to get, if any</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The resource with the specified name, if any</returns>
    Task<TResource?> GetResourceAsync<TResource>(string name, string? @namespace = null, CancellationToken cancellationToken = default)
        where TResource : class, IResource, new();

    /// <summary>
    /// Lists resources of the specified type
    /// </summary>
    /// <typeparam name="TResource">The type of resources to list</typeparam>
    /// <param name="namespace">The namespace the resources to list belong to, if any</param>
    /// <param name="labelSelectors">An <see cref="IEnumerable{T}"/> containing all label-based selectors to filter resources by</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IList{T}"/> containing the resources matching the query</returns>
    Task<IAsyncEnumerable<TResource>?> ListResourcesAsync<TResource>(string? @namespace = null, IEnumerable<ResourceLabelSelector>? labelSelectors = null, CancellationToken cancellationToken = default)
        where TResource : class, IResource, new();

    /// <summary>
    /// Updates the specified <see cref="IResource"/>
    /// </summary>
    /// <typeparam name="TResource">The type of <see cref="IResource"/> to update</typeparam>
    /// <param name="resource">The updated <see cref="IResource"/></param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The updated <see cref="IResource"/></returns>
    Task<TResource> UpdateResourceAsync<TResource>(TResource resource, CancellationToken cancellationToken = default)
        where TResource : class, IResource, new();

    /// <inheritdoc/>
    Task<TResource?> PatchResourceAsync<TResource>(Patch patch, string name, string? @namespace = null, CancellationToken cancellationToken = default)
        where TResource : class, IResource, new();

    /// <inheritdoc/>
    Task<TResource?> PatchResourceStatusAsync<TResource>(Patch patch, string name, string? @namespace = null, CancellationToken cancellationToken = default)
        where TResource : class, IResource, new();

    /// <summary>
    /// Updates the specified <see cref="IResource"/>'s status
    /// </summary>
    /// <typeparam name="TResource">The type of <see cref="IResource"/> to update</typeparam>
    /// <param name="resource">The updated <see cref="IResource"/></param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The updated <see cref="IResource"/></returns>
    Task<TResource> UpdateResourceStatusAsync<TResource>(TResource resource, CancellationToken cancellationToken = default)
        where TResource : class, IResource, new();

    /// <summary>
    /// Watches resources of the specified type
    /// </summary>
    /// <typeparam name="TResource">The type of resource to watch</typeparam>
    /// <param name="namespace">The namespace the resources to watch belong to, if any</param>
    /// <param name="labelSelectors">An <see cref="IEnumerable{T}"/> containing all label-based selectors to filter resources to watch by</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IObservable{T}"/></returns>
    Task<IObservable<IResourceWatchEvent<TResource>>> WatchResourcesAsync<TResource>(string? @namespace = null, IEnumerable<ResourceLabelSelector>? labelSelectors = null, CancellationToken cancellationToken = default)
        where TResource : class, IResource, new();

}