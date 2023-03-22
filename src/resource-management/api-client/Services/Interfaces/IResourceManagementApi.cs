using CloudStreams.Core.Data.Models;

namespace CloudStreams.ResourceManagement.Api.Client.Services;

/// <summary>
/// Defines the fundamentals of the Cloud Streams API used to manage <see cref="IResource"/>s
/// </summary>
public interface IResourceManagementApi<TResource>
    where TResource : class, IResource, new()
{

    /// <summary>
    /// Creates a new <see cref="IResource"/>
    /// </summary>
    /// <param name="resource">The <see cref="IResource"/> to create</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    Task<TResource> CreateAsync(TResource resource, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the <see cref="ResourceDefinition"/> for the managed <see cref="IResource"/> type
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The <see cref="ResourceDefinition"/> for the managed <see cref="IResource"/> type</returns>
    Task<ResourceDefinition> GetDefinitionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the specified <see cref="IResource"/>
    /// </summary>
    /// <param name="name">The name of the <see cref="IResource"/> to get</param>
    /// <param name="namespace">The namespace the <see cref="IResource"/> to get belongs to</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The specified <see cref="IResource"/></returns>
    Task<TResource> GetAsync(string name, string? @namespace = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists <see cref="IResource"/>s
    /// </summary>
    /// <param name="namespace">The namespace the <see cref="IResource"/>s to list belong to</param>
    /// <param name="labelSelectors">An <see cref="IEnumerable{T}"/> containing the <see cref="ResourceLabelSelector"/>s used to select the <see cref="IResource"/>s to list by, if any</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IAsyncEnumerable{T}"/> used to asynchronously enumerate resulting <see cref="IResource"/>s</returns>
    Task<IAsyncEnumerable<TResource>> ListAsync(string? @namespace = null, IEnumerable<ResourceLabelSelector>? labelSelectors = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the specified <see cref="IResource"/>
    /// </summary>
    /// <param name="resource">The <see cref="IResource"/> to update</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The updated <see cref="IResource"/></returns>
    Task<TResource> UpdateAsync(TResource resource, CancellationToken cancellationToken = default);

    /// <summary>
    /// Patches the specified <see cref="IResource"/>
    /// </summary>
    /// <param name="patch">The <see cref="ResourcePatch{TResource}"/> to apply</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The patched <see cref="IResource"/></returns>
    Task<TResource> PatchAsync(ResourcePatch<TResource> patch, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the specified <see cref="IResource"/>
    /// </summary>
    /// <param name="name">The name of the <see cref="IResource"/> to delete</param>
    /// <param name="namespace">The namespace the <see cref="IResource"/> to delete belongs to</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    Task DeleteAsync(string name, string? @namespace = null, CancellationToken cancellationToken = default);

}
