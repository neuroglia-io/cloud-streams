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

namespace CloudStreams.Core;

/// <summary>
/// Defines extensions for <see cref="IResourceReference"/>s
/// </summary>
public static class IResourceReferenceExtensions
{

    /// <summary>
    /// Gets the group the referenced <see cref="IResource"/> belongs to
    /// </summary>
    /// <param name="reference">The extended <see cref="IResourceReference"/></param>
    /// <returns>The group the referenced <see cref="IResource"/> belongs to</returns>
    public static string GetGroup(this IResourceReference reference) => reference.ApiVersion.Split('/')[0];

    /// <summary>
    /// Gets the referenced <see cref="IResource"/>'s version
    /// </summary>
    /// <param name="reference">The extended <see cref="IResourceReference"/></param>
    /// <returns>The referenced <see cref="IResource"/>'s version</returns>
    public static string GetVersion(this IResourceReference reference) => reference.ApiVersion.Split('/')[1];

    /// <summary>
    /// Determines whether or not the <see cref="IResourceReference"/> refers to a namespaced <see cref="IResource"/>
    /// </summary>
    /// <param name="reference">The <see cref="IResourceReference"/> to check</param>
    /// <returns>A boolean indicating whether or not the <see cref="IResourceReference"/> refers to a namespaced <see cref="IResource"/></returns>
    public static bool IsNamespaced(this IResourceReference reference) => !string.IsNullOrWhiteSpace(reference.Namespace);

}