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

namespace CloudStreams.Core.JsonResourceExtensions;

/// <summary>
/// Defines extensions for JSON-based resources
/// </summary>
public static class JsonResourceExtensions
{

    /// <summary>
    /// Determines whether or not the <see cref="JsonObject"/> describes an <see cref="IResource"/>
    /// </summary>
    /// <param name="jsonObject">The <see cref="JsonObject"/> to check</param>
    /// <returns>A boolean indicating whether or not the <see cref="JsonObject"/> describes an <see cref="IResource"/></returns>
    public static bool IsResource(this JsonObject jsonObject)
    {
        if (!jsonObject.TryGetPropertyValue(nameof(IResource.ApiVersion).ToCamelCase(), out _) 
            || !jsonObject.TryGetPropertyValue(nameof(IResource.Kind).ToCamelCase(), out _)
            || !jsonObject.TryGetPropertyValue(nameof(IResource.Metadata).ToCamelCase(), out _)) 
            return false;
        else return true;
    }

    /// <summary>
    /// Determines whether or not the <see cref="JsonObject"/> represents an <see cref="IResource"/> of the specified type
    /// </summary>
    /// <param name="jsonObject">The <see cref="JsonObject"/> to check</param>
    /// <param name="type">The expected type of <see cref="IResource"/></param>
    /// <returns>A boolean indicating whether or not the <see cref="JsonObject"/> represents an <see cref="IResource"/> of the specified type</returns>
    public static bool IsOfType(this JsonObject jsonObject, ResourceType type)
    {
        if (type == null) throw new ArgumentNullException(nameof(type));
        if (!jsonObject.TryGetPropertyValue(nameof(IResource.ApiVersion).ToCamelCase(), out var apiVersionNode) || apiVersionNode == null) return false;
        if (!jsonObject.TryGetPropertyValue(nameof(IResource.Kind).ToCamelCase(), out var kindNode) || kindNode == null) return false;
        var apiVersion = Serializer.Json.Deserialize<string>(apiVersionNode);
        var kind = Serializer.Json.Deserialize<string>(kindNode);
        return apiVersion == type.GetApiVersion() && kind == type.Kind;
    }

    /// <summary>
    /// Determines whether or not the <see cref="JsonObject"/> represents an <see cref="IResource"/> of the specified type
    /// </summary>
    /// <typeparam name="TResource">The expected type of <see cref="IResource"/></typeparam>
    /// <param name="jsonObject">The <see cref="JsonObject"/> to check</param>
    /// <returns>A boolean indicating whether or not the <see cref="JsonObject"/> represents an <see cref="IResource"/> of the specified type</returns>
    public static bool IsOfType<TResource>(this JsonObject jsonObject)
        where TResource : class, IResource, new()
    {
        return jsonObject.IsOfType(new TResource().Type);
    }

}
