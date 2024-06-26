﻿// Copyright © 2024-Present The Cloud Streams Authors
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

using Neuroglia.Data;

namespace CloudStreams.Core.Api.Client.Services;

/// <summary>
/// Defines the fundamentals of the Cloud Streams API used to manage <see cref="IResource"/>s
/// </summary>
public interface IResourceManagementApi<TResource>
    where TResource : IResource, new()
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
    /// <param name="labelSelectors">An <see cref="IEnumerable{T}"/> containing the <see cref="LabelSelector"/>s used to select the <see cref="IResource"/>s to list by, if any</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IAsyncEnumerable{T}"/> used to asynchronously enumerate resulting <see cref="IResource"/>s</returns>
    Task<IAsyncEnumerable<TResource>> ListAsync(string? @namespace = null, IEnumerable<LabelSelector>? labelSelectors = null, CancellationToken cancellationToken = default);

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
    /// <param name="patch">The <see cref="Patch"/> to apply</param>
    /// <param name="name">The name of the resource to patch</param>
    /// <param name="namespace">The namespace the resource to patch belongs to, if any</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The patched <see cref="IResource"/></returns>
    Task<TResource> PatchAsync(Patch patch, string name, string? @namespace = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Patches the status of the specified <see cref="IResource"/>
    /// </summary>
    /// <param name="patch">The <see cref="Patch"/> to apply</param>
    /// <param name="name">The name of the resource to patch</param>
    /// <param name="namespace">The namespace the resource to patch belongs to, if any</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The patched resource</returns>
    Task<TResource> PatchStatusAsync(Patch patch, string name, string? @namespace = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the specified <see cref="IResource"/>
    /// </summary>
    /// <param name="name">The name of the <see cref="IResource"/> to delete</param>
    /// <param name="namespace">The namespace the <see cref="IResource"/> to delete belongs to</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    Task DeleteAsync(string name, string? @namespace = null, CancellationToken cancellationToken = default);

}
