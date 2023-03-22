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

using CloudStreams.Core.Data.Models;

namespace CloudStreams.Core;

/// <summary>
/// Defines extensions for <see cref="IResourceWatchEvent"/>s
/// </summary>
public static class ResourceWatchEventExtensions
{

    /// <summary>
    /// Converts the <see cref="ResourceWatchEvent"/> into a new <see cref="ResourceWatchEvent{TResource}"/>
    /// </summary>
    /// <typeparam name="TResource">The type of watched <see cref="Resource"/>s</typeparam>
    /// <param name="e">The <see cref="ResourceWatchEvent"/> to convert</param>
    /// <returns>A new <see cref="ResourceWatchEvent{TResource}"/></returns>
    public static ResourceWatchEvent<TResource> ToType<TResource>(this ResourceWatchEvent e)
        where TResource : class, IResource, new()
    {
        var resource = Serializer.Json.Deserialize<TResource>(Serializer.Json.Serialize(e.Resource))!;
        return new ResourceWatchEvent<TResource>(e.Type, resource);
    }

}
