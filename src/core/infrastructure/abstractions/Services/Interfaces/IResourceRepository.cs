namespace CloudStreams.Infrastructure.Services;

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
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IList{T}"/> containing the resources matching the query</returns>
    Task<IList<TResource>?> ListResourcesAsync<TResource>(string? @namespace, CancellationToken cancellationToken = default)
        where TResource : class, IResource, new();

    /// <summary>
    /// Watches resources of the specified type
    /// </summary>
    /// <typeparam name="TResource">The type of resource to watch</typeparam>
    /// <returns>A new <see cref="IObservable{T}"/></returns>
    Task<IObservable<IResourceWatchEvent<TResource>>> WatchResourcesAsync<TResource>(string? @namespace = null, CancellationToken cancellationToken = default)
        where TResource : class, IResource, new();

}