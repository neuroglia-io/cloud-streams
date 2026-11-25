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

using System.Text.Json.Nodes;

namespace CloudStreams.Core.Application.Services;

/// <summary>
/// Checks the compatibility of the versions of a JSON schema (see #213)
/// </summary>
/// <remarks>
/// <para>
/// A version is backward compatible with the previous one if it accepts every document the previous one accepts, and forward compatible if the previous one accepts
/// every document it accepts. Each constraint the accepting schema imposes must therefore be implied by the other schema.
/// </para>
/// <para>
/// The check understands the keywords most schemas evolve with: <c>type</c>, <c>enum</c>, <c>const</c>, <c>required</c>, <c>properties</c>,
/// <c>additionalProperties</c>, <c>items</c>, and the numeric, length, item and property count bounds. Any other constraint, such as <c>pattern</c>, <c>format</c>,
/// <c>$ref</c> or <c>oneOf</c>, is only deemed implied if both schemas define it identically: the check errs on the side of refusing a change it cannot verify.
/// Annotations, such as <c>title</c> or <c>description</c>, are ignored.
/// </para>
/// </remarks>
public static class SchemaCompatibilityChecker
{

    static readonly HashSet<string> Annotations = ["$schema", "$id", "$comment", "$defs", "definitions", "title", "description", "default", "examples", "deprecated", "readOnly", "writeOnly", "$anchor"];

    static readonly HashSet<string> Verified = ["type", "enum", "const", "required", "properties", "additionalProperties", "items",
        "minimum", "maximum", "exclusiveMinimum", "exclusiveMaximum", "minLength", "maxLength", "minItems", "maxItems", "minProperties", "maxProperties"];

    static readonly string[] LowerBounds = ["minimum", "exclusiveMinimum", "minLength", "minItems", "minProperties"];

    static readonly string[] UpperBounds = ["maximum", "exclusiveMaximum", "maxLength", "maxItems", "maxProperties"];

    /// <summary>
    /// Checks that the specified version of a schema is compatible with the previous one
    /// </summary>
    /// <param name="previous">The previous version of the schema</param>
    /// <param name="next">The next version of the schema</param>
    /// <param name="compatibility">The compatibility to check. See <see cref="SchemaCompatibility"/></param>
    /// <returns>The reasons why the next version is not compatible with the previous one, none if it is</returns>
    public static IReadOnlyList<string> Check(JsonNode? previous, JsonNode? next, string compatibility)
    {
        var errors = new List<string>();
        switch (compatibility)
        {
            case SchemaCompatibility.None:
                break;
            case SchemaCompatibility.Backward:
                Accepts(next, previous, string.Empty, errors, "a document the previous version accepts would be refused");
                break;
            case SchemaCompatibility.Forward:
                Accepts(previous, next, string.Empty, errors, "a document the new version accepts would be refused by the previous one");
                break;
            case SchemaCompatibility.Full:
                Accepts(next, previous, string.Empty, errors, "a document the previous version accepts would be refused");
                Accepts(previous, next, string.Empty, errors, "a document the new version accepts would be refused by the previous one");
                break;
            default:
                throw new NotSupportedException($"The schema compatibility '{compatibility}' is not supported");
        }
        return errors;
    }

    /// <summary>
    /// Checks that the accepting schema accepts every document the other schema accepts
    /// </summary>
    static void Accepts(JsonNode? accepting, JsonNode? accepted, string path, List<string> errors, string consequence)
    {
        void Fail(string reason) => errors.Add($"{consequence}, because at '{(path.Length == 0 ? "/" : path)}' {reason}");
        if (IsTrue(accepting) || IsFalse(accepted)) return;
        if (IsFalse(accepting))
        {
            Fail("no document is accepted anymore");
            return;
        }
        var wider = (JsonObject)accepting!;
        var narrower = accepted as JsonObject ?? [];
        foreach (var (keyword, constraint) in wider)
        {
            if (Annotations.Contains(keyword) || Verified.Contains(keyword)) continue;
            if (!narrower.TryGetPropertyValue(keyword, out var other) || !JsonNode.DeepEquals(constraint, other)) Fail($"the '{keyword}' constraint has changed, which the compatibility check cannot verify");
        }
        // type
        var acceptingTypes = Types(wider);
        if (acceptingTypes != null)
        {
            var acceptedTypes = Types(narrower);
            if (acceptedTypes == null) Fail($"the type is restricted to {string.Join(", ", acceptingTypes.Select(t => $"'{t}'"))}");
            else foreach (var type in acceptedTypes.Where(t => !acceptingTypes.Contains(t) && !(t == "integer" && acceptingTypes.Contains("number")))) Fail($"the type '{type}' is no longer accepted");
        }
        // enum and const
        var acceptingValues = Values(wider);
        if (acceptingValues != null)
        {
            var acceptedValues = Values(narrower);
            if (acceptedValues == null) Fail("the values are restricted to an enumeration");
            else foreach (var value in acceptedValues.Where(v => !acceptingValues.Any(a => JsonNode.DeepEquals(a, v)))) Fail($"the value {value?.ToJsonString() ?? "null"} is no longer accepted");
        }
        // bounds
        foreach (var bound in LowerBounds)
        {
            if (!TryGetNumber(wider, bound, out var minimum)) continue;
            if (!TryGetNumber(narrower, bound, out var other) || other < minimum) Fail($"'{bound}' has been raised to {minimum}");
        }
        foreach (var bound in UpperBounds)
        {
            if (!TryGetNumber(wider, bound, out var maximum)) continue;
            if (!TryGetNumber(narrower, bound, out var other) || other > maximum) Fail($"'{bound}' has been lowered to {maximum}");
        }
        // required
        var acceptedRequired = Strings(narrower["required"]);
        foreach (var property in Strings(wider["required"]).Where(p => !acceptedRequired.Contains(p))) Fail($"the property '{property}' is required");
        // properties
        var acceptingProperties = wider["properties"] as JsonObject ?? [];
        var acceptedProperties = narrower["properties"] as JsonObject ?? [];
        var acceptedAdditional = narrower["additionalProperties"];
        foreach (var (property, schema) in acceptingProperties)
        {
            // a property the other schema does not declare is constrained by its additional properties, if it allows any
            var acceptedSchema = acceptedProperties.TryGetPropertyValue(property, out var declared) ? declared : acceptedAdditional;
            if (!acceptedProperties.ContainsKey(property) && IsFalse(acceptedAdditional)) continue;
            Accepts(schema, acceptedSchema, $"{path}/properties/{property}", errors, consequence);
        }
        var acceptingAdditional = wider["additionalProperties"];
        if (acceptingAdditional != null && !IsTrue(acceptingAdditional))
        {
            foreach (var (property, schema) in acceptedProperties.Where(p => !acceptingProperties.ContainsKey(p.Key)))
            {
                if (IsFalse(acceptingAdditional)) Fail($"the property '{property}' is no longer allowed");
                else Accepts(acceptingAdditional, schema, $"{path}/properties/{property}", errors, consequence);
            }
            if (!IsFalse(acceptedAdditional))
            {
                if (IsFalse(acceptingAdditional)) Fail("additional properties are no longer allowed");
                else Accepts(acceptingAdditional, acceptedAdditional, $"{path}/additionalProperties", errors, consequence);
            }
        }
        // items
        if (wider["items"] is JsonNode items) Accepts(items, narrower["items"], $"{path}/items", errors, consequence);
    }

    static bool IsTrue(JsonNode? schema) => schema == null || (schema is JsonValue value && value.TryGetValue<bool>(out var b) && b) || (schema is JsonObject o && o.All(p => Annotations.Contains(p.Key)));

    static bool IsFalse(JsonNode? schema) => schema is JsonValue value && value.TryGetValue<bool>(out var b) && !b;

    static HashSet<string>? Types(JsonObject schema) => schema["type"] switch
    {
        JsonValue value when value.TryGetValue<string>(out var type) => [type],
        JsonArray array => [.. Strings(array)],
        _ => null
    };

    static List<JsonNode?>? Values(JsonObject schema)
    {
        if (schema.TryGetPropertyValue("const", out var constant)) return [constant];
        return schema["enum"] is JsonArray values ? [.. values] : null;
    }

    static HashSet<string> Strings(JsonNode? node) => node is JsonArray array ? [.. array.OfType<JsonValue>().Select(v => v.TryGetValue<string>(out var s) ? s : null).OfType<string>()] : [];

    static bool TryGetNumber(JsonObject schema, string keyword, out decimal number)
    {
        number = 0;
        if (schema[keyword] is not JsonValue value) return false;
        if (value.TryGetValue(out number)) return true;
        if (value.TryGetValue<long>(out var integer)) { number = integer; return true; }
        if (value.TryGetValue<double>(out var real)) { number = (decimal)real; return true; }
        return false;
    }

}
