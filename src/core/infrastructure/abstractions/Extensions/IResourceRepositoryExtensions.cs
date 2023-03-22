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

using CloudStreams.Core.Infrastructure.Services;

namespace CloudStreams.Core.Infrastructure;

/// <summary>
/// Defines extensions for <see cref="IResourceMonitor{TResource}"/> instances
/// </summary>
public static class IResourceRepositoryExtensions
{

    /// <summary>
    /// Creates a new <see cref="IResourceMonitor{TResource}"/> to monitor the state of the specified resource
    /// </summary>
    /// <typeparam name="TResource">The type of the resource to monitor</typeparam>
    /// <param name="repository">The extended <see cref="IResourceRepository"/></param>
    /// <param name="name">The name of the resource to monitor</param>
    /// <param name="namespace">The namespace the resource to monitor belongs to, if any</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IResourceMonitor{TResource}"/></returns>
    public static async Task<IResourceMonitor<TResource>> MonitorAsync<TResource>(this IResourceRepository repository, string name, string? @namespace = null, CancellationToken cancellationToken = default)
        where TResource : class, IResource, new()
    {
        var state = await repository.GetResourceAsync<TResource>(name, @namespace, cancellationToken);
        if (state == null) throw ApplicationException.ResourceNotFound<TResource>(name, @namespace);
        var monitor = new ResourceMonitor<TResource>(repository, state);
        await monitor.StartAsync(cancellationToken);
        return monitor;
    }

    /// <summary>
    /// Patches the specified <see cref="IResource"/>
    /// </summary>
    /// <typeparam name="TResource">The type of <see cref="IResource"/> to patch</typeparam>
    /// <param name="repository">The <see cref="IResourceRepository"/> used to manage the resource to patch</param>
    /// <param name="patch">The <see cref="ResourcePatch{TResource}"/> to apply</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The patched <see cref="IResource"/>, if any</returns>
    public static Task<TResource?> PatchResourceAsync<TResource>(this IResourceRepository repository, ResourcePatch<TResource> patch, CancellationToken cancellationToken = default)
        where TResource : class, IResource, new()
    {
        return repository.PatchResourceAsync<TResource>(patch.Patch, patch.Resource.Name, patch.Resource.Namespace, cancellationToken);
    }

}
