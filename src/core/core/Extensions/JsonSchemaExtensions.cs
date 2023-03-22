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

using CloudStreams.Core.Serialization.Json.Utilities;

namespace CloudStreams.Core;

/// <summary>
/// Defines extensions for <see cref="JsonSchema"/>s
/// </summary>
public static class JsonSchemaExtensions
{

    /// <summary>
    /// Gets the <see cref="JsonSchema"/> that defines the property with the specified name
    /// </summary>
    /// <param name="schema">The <see cref="JsonSchema"/> to check for the specified property</param>
    /// <param name="propertyName">The name of the property to get the <see cref="JsonSchema"/> of</param>
    /// <returns>The <see cref="JsonSchema"/> that defines the property with the specified name, if any</returns>
    public static JsonSchema? GetProperty(this JsonSchema schema, string propertyName)
    {
        var properties = schema.Keywords?.OfType<PropertiesKeyword>().SingleOrDefault()?.Properties;
        properties ??= schema.Keywords?.OfType<ItemsKeyword>().SingleOrDefault()?.SingleSchema?.Keywords?.OfType<PropertiesKeyword>().SingleOrDefault()?.Properties;
        if (properties == null || !properties.TryGetValue(propertyName, out var propertySchema)) return null;
        var items = propertySchema?.Keywords?.OfType<ItemsKeyword>().SingleOrDefault();
        if (items?.SingleSchema != null) propertySchema = items.SingleSchema;
        return propertySchema;
    }

    /// <summary>
    /// Gets the <see cref="JsonStrategicMergePatch"/> merge key defined by the <see cref="JsonSchema"/>, if any
    /// </summary>
    /// <param name="schema">The <see cref="JsonSchema"/> to check</param>
    /// <returns>The <see cref="JsonStrategicMergePatch"/> merge key defined by the <see cref="JsonSchema"/>, if any</returns>
    public static string? GetPatchMergeKey(this JsonSchema schema)
    {
        return schema.Keywords?.OfType<UnrecognizedKeyword>().SingleOrDefault(k => k.Name == JsonStrategicMergePatch.JsonSchemaProperties.MergeKey)?.Value?.GetValue<string>();
    }

}