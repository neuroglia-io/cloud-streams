// Copyright © 2023-Present The Cloud Streams Authors
//
// Licensed under the Apache License, Version 2.0 (the "License"),
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

namespace CloudStreams.Core.Infrastructure.Services;

/// <summary>
/// Defines the fundamentals of a service used to manage the application's resources
/// </summary>
public interface IResourceRepository
{

    /// <summary>
    /// Creates and persist the specified resource to the repository
    /// </summary>
    /// <typeparam name="TResource">The type of resource to create</typeparam>
    /// <param name="resource">The resource to create</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The added resource</returns>
    Task<TResource> CreateResourceAsync<TResource>(TResource resource, CancellationToken cancellationToken = default)
        where TResource : class, IResource, new();

    /// <summary>
    /// Gets the definition of the specified resource type
    /// </summary>
    /// <typeparam name="TResource">The type of resource to get the definition of</typeparam>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    Task<IResourceDefinition?> GetResourceDefinitionAsync<TResource>(CancellationToken cancellationToken = default)
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

    /// <summary>
    /// Patches the specified <see cref="IResource"/>
    /// </summary>
    /// <typeparam name="TResource">The type of <see cref="IResource"/> to patch</typeparam>
    /// <param name="patch">The patch to apply</param>
    /// <param name="name">The name of the <see cref="IResource"/> to patch</param>
    /// <param name="namespace">The namespace the <see cref="IResource"/> belongs to, if any</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The patched <see cref="IResource"/></returns>
    Task<TResource?> PatchResourceAsync<TResource>(Patch patch, string name, string? @namespace = null, CancellationToken cancellationToken = default)
        where TResource : class, IResource, new();
    
    /// <summary>
    /// Patches the specified <see cref="IResource"/>'s status
    /// </summary>
    /// <typeparam name="TResource">The type of <see cref="IResource"/> to patch the status of</typeparam>
    /// <param name="patch">The patch to apply</param>
    /// <param name="name">The name of the <see cref="IResource"/> to patch</param>
    /// <param name="namespace">The namespace the <see cref="IResource"/> belongs to, if any</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The patched <see cref="IResource"/></returns>
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
    /// Removes the specified <see cref="IResource"/>
    /// </summary>
    /// <param name="name">The name of the <see cref="IResource"/> to remove</param>
    /// <param name="namespace">The namespace the <see cref="IResource"/> to remove belongs to</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    Task DeleteResourceAsync<TResource>(string name, string? @namespace = null, CancellationToken cancellationToken = default)
        where TResource : class, IResource, new();

    /// <summary>
    /// Watches resources of the specified type
    /// </summary>
    /// <param name="group">The group the resources to watch belong to</param>
    /// <param name="version">The version of the resources to watch</param>
    /// <param name="plural">The plural name of the type of resource to watch</param>
    /// <param name="namespace">The namespace the resources to watch belong to, if any</param>
    /// <param name="labelSelectors">An <see cref="IEnumerable{T}"/> containing all label-based selectors to filter resources to watch by</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IObservable{T}"/></returns>
    Task<IResourceWatcher> WatchResourcesAsync(string group, string version, string plural, string? @namespace = null, IEnumerable<ResourceLabelSelector>? labelSelectors = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Watches resources of the specified type
    /// </summary>
    /// <typeparam name="TResource">The type of resource to watch</typeparam>
    /// <param name="namespace">The namespace the resources to watch belong to, if any</param>
    /// <param name="labelSelectors">An <see cref="IEnumerable{T}"/> containing all label-based selectors to filter resources to watch by</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IObservable{T}"/></returns>
    Task<IResourceWatcher<TResource>> WatchResourcesAsync<TResource>(string? @namespace = null, IEnumerable<ResourceLabelSelector>? labelSelectors = null, CancellationToken cancellationToken = default)
        where TResource : class, IResource, new();

}