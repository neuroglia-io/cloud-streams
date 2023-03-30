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

using Json.Patch;

namespace CloudStreams.Core;

/// <summary>
/// Exposes methods to help handling <see cref="JsonPatch"/>es
/// </summary>
public static class JsonPatchHelper
{

    /// <summary>
    /// Creates a new <see cref="JsonPatch"/> that represents the difference between the specified source and target
    /// </summary>
    /// <param name="source">The original state</param>
    /// <param name="target">The updated state</param>
    /// <returns>A new <see cref="JsonPatch"/> that represents the difference between the specified source and target</returns>
    public static JsonPatch CreateJsonPatch(object source, object target)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (target == null) throw new ArgumentNullException(nameof(target));
        return Serializer.Json.SerializeToNode(source).CreatePatch(Serializer.Json.SerializeToNode(target));
    }

}