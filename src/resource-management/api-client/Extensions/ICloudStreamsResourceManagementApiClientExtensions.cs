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

using CloudStreams.ResourceManagement.Api.Client.Services;

namespace CloudStreams;

/// <summary>
/// Defines extensions for <see cref="ICloudStreamsResourceManagementApiClient"/>s
/// </summary>
public static class ICloudStreamsResourceManagementApiClientExtensions
{

    /// <summary>
    /// Gets the <see cref="IResourceManagementApi{TResource}"/> for the specified <see cref="IResource"/> type
    /// </summary>
    /// <typeparam name="TResource">The type of <see cref="IResource"/> to get the <see cref="IResourceManagementApi{TResource}"/> for</typeparam>
    /// <param name="client">The extended <see cref="ICloudStreamsResourceManagementApiClient"/></param>
    /// <returns>The <see cref="IResourceManagementApi{TResource}"/> for the specified <see cref="IResource"/> type</returns>
    public static IResourceManagementApi<TResource> Manage<TResource>(this ICloudStreamsResourceManagementApiClient client)
        where TResource : class, IResource, new()
    {
        var apiProperty = client.GetType().GetProperties().SingleOrDefault(p => p.CanRead && typeof(IResourceManagementApi<>).MakeGenericType(typeof(TResource)).IsAssignableFrom(p.PropertyType));
        if (apiProperty == null) throw new NullReferenceException($"Failed to find a management API for the specified resource type '{new TResource().Type}'");
        return (IResourceManagementApi<TResource>)apiProperty.GetValue(client)!;
    }

}