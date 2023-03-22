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
/// Defines extensions for <see cref="JsonNode"/>s
/// </summary>
public static class JsonNodeExtensions
{

    /// <summary>
    /// Converts the <see cref="JsonNode"/> into a <see cref="JsonElement"/>
    /// </summary>
    /// <param name="jsonNode">The <see cref="JsonNode"/> to convert</param>
    /// <returns>A new <see cref="JsonElement"/></returns>
    public static JsonElement AsJsonElement(this JsonNode jsonNode)
    {
        return jsonNode.Deserialize<JsonElement>();
    }

}
