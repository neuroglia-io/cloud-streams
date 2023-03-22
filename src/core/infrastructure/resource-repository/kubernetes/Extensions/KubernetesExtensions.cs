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
using k8s;
using System.Text.Json;

namespace CloudStreams.Core.Infrastructure;

/// <summary>
/// Defines extensions for <see cref="Kubernetes"/> instances
/// </summary>
public static class KubernetesExtensions
{

    /// <summary>
    /// Lists <see cref="IResource"/>s of the specified type
    /// </summary>
    /// <typeparam name="TResource">The type of <see cref="IResource"/> to list</typeparam>
    /// <param name="kubernetes">The extended <see cref="Kubernetes"/></param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="CustomResourceList{T}"/>, that wraps the query results</returns>
    public static async Task<CustomResourceList<TResource>?> ListClusterCustomObjectAsync<TResource>(this Kubernetes kubernetes, CancellationToken cancellationToken = default)
        where TResource : class, IResource, new()
    {
        var resource = new TResource();
        var group = resource.Type.Group;
        var version = resource.Type.Version;
        var plural = resource.Type.Plural;
        JsonElement? resourceObjectArray;
        resourceObjectArray = (JsonElement)await kubernetes.CustomObjects.ListClusterCustomObjectAsync(group, version, plural, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (resourceObjectArray == null) return null;
        return Serializer.Json.Deserialize<CustomResourceList<TResource>>((JsonElement)resourceObjectArray);
    }

}
