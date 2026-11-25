// Copyright © 2024-Present The Cloud Streams Authors
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

using System.Text.Json;
using System.Text.Json.Nodes;

namespace CloudStreams.Dashboard.Services;

/// <summary>
/// Infers a JSON schema from a sample document, as a starting point for a schema to register (see #264)
/// </summary>
/// <remarks>
/// The schema describes the types of the sample: the properties it has are required, and arrays are described by their first item. It does not set
/// <c>additionalProperties</c>, so that it remains open, as schemas usually start
/// </remarks>
public static class JsonSchemaInference
{

    /// <summary>
    /// Gets the dialect of the inferred schemas
    /// </summary>
    public const string Dialect = "https://json-schema.org/draft/2020-12/schema";

    /// <summary>
    /// Infers a JSON schema from the specified sample document
    /// </summary>
    /// <param name="sample">The sample document</param>
    /// <returns>A new JSON schema that the sample is valid against</returns>
    public static JsonObject Infer(JsonNode? sample)
    {
        var schema = InferNode(sample);
        var root = new JsonObject() { ["$schema"] = Dialect };
        foreach (var (key, value) in schema) root[key] = value?.DeepClone();
        return root;
    }

    static JsonObject InferNode(JsonNode? node)
    {
        switch (node)
        {
            case null:
                return new() { ["type"] = "null" };
            case JsonObject obj:
                var properties = new JsonObject();
                var required = new JsonArray();
                foreach (var (key, value) in obj)
                {
                    properties[key] = InferNode(value);
                    if (value != null) required.Add(key);
                }
                var schema = new JsonObject() { ["type"] = "object", ["properties"] = properties };
                if (required.Count > 0) schema["required"] = required;
                return schema;
            case JsonArray array:
                return array.Count == 0 ? new() { ["type"] = "array" } : new() { ["type"] = "array", ["items"] = InferNode(array[0]) };
            case JsonValue value:
                return value.GetValueKind() switch
                {
                    JsonValueKind.String => new() { ["type"] = "string" },
                    JsonValueKind.True or JsonValueKind.False => new() { ["type"] = "boolean" },
                    JsonValueKind.Number => new() { ["type"] = value.TryGetValue<long>(out _) || (value.TryGetValue<double>(out var number) && Math.Floor(number) == number) ? "integer" : "number" },
                    _ => new() { ["type"] = "null" }
                };
            default:
                return [];
        }
    }

}
