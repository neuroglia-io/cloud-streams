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

using Neuroglia.Serialization.Yaml;
using System.Text.Json;
using System.Text.Json.Nodes;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace CloudStreams.Core.Kubernetes;

/// <summary>
/// Generates the Kubernetes CustomResourceDefinitions of the resources of Cloud Streams from their definitions, which Kubernetes stores and serves them with in
/// Kubernetes mode (see #276)
/// </summary>
/// <remarks>
/// The definitions of Cloud Streams are written as CustomResourceDefinitions, whose schemas describe their resources as precisely as the dashboard's editor can use
/// them. Kubernetes only accepts structural schemas, which forbid part of that precision: the generated CRDs are the definitions, whose schemas are rewritten as
/// structural ones. What they no longer check, such as a field that is either an address or an object, is checked by the validators of Cloud Streams
/// </remarks>
public static class CustomResourceDefinitionGenerator
{

    /// <summary>
    /// Gets the keywords that a schema nested in a logical junctor (<c>oneOf</c>, <c>anyOf</c>, <c>allOf</c> or <c>not</c>) may not declare in a structural schema
    /// </summary>
    static readonly string[] KeywordsForbiddenInJunctors = ["type", "description", "default", "nullable", "title", "additionalProperties", "x-kubernetes-preserve-unknown-fields",
        "x-kubernetes-int-or-string", "x-kubernetes-embedded-resource", "x-kubernetes-list-type", "x-kubernetes-list-map-keys", "x-kubernetes-map-type"];

    static readonly string[] Junctors = ["oneOf", "anyOf", "allOf"];

    /// <summary>
    /// Gets the columns <c>kubectl get</c> prints for the resources of each kind, besides their name
    /// </summary>
    static readonly Dictionary<string, JsonArray> PrinterColumns = new()
    {
        ["Subscription"] =
        [
            Column("Phase", "string", ".status.phase"),
            Column("Subscriber", "string", ".status.subscriber.state"),
            Column("Acked", "integer", ".status.stream.ackedOffset", "The offset of the next event to deliver"),
            Column("Suspended", "boolean", ".spec.suspended"),
            Column("Age", "date", ".metadata.creationTimestamp")
        ],
        // the readiness and the replicas of the workloads the operator deploys (see #279)
        ["Gateway"] =
        [
            Column("Ready", "string", ".status.conditions[?(@.type==\"Ready\")].status", "Whether the gateway's workload is ready, when the operator deploys it"),
            Column("Available", "integer", ".status.availableReplicas", "The available replicas of the gateway's workload, when the operator deploys it"),
            Column("Health", "string", ".status.healthStatus"),
            Column("Age", "date", ".metadata.creationTimestamp")
        ],
        ["Broker"] =
        [
            Column("Ready", "string", ".status.conditions[?(@.type==\"Ready\")].status", "Whether the broker's workload is ready, when the operator deploys it"),
            Column("Available", "integer", ".status.availableReplicas", "The available replicas of the broker's workload, when the operator deploys it"),
            Column("Health", "string", ".status.healthStatus"),
            Column("Age", "date", ".metadata.creationTimestamp")
        ],
        ["Sink"] = [Column("Endpoint", "string", ".spec.endpoint"), Column("Age", "date", ".metadata.creationTimestamp")],
        ["Feed"] = [Column("Endpoint", "string", ".spec.endpoint"), Column("Age", "date", ".metadata.creationTimestamp")],
        ["Schema"] = [Column("Compatibility", "string", ".spec.compatibility"), Column("Age", "date", ".metadata.creationTimestamp")]
    };

    /// <summary>
    /// Generates the CustomResourceDefinitions of every resource of Cloud Streams
    /// </summary>
    /// <returns>The name of each definition's file, e.g. <c>subscriptions.cloud-streams.io.yaml</c>, and its YAML</returns>
    public static IEnumerable<(string FileName, string Yaml)> GenerateAll()
    {
        var assembly = typeof(CustomResourceDefinitionGenerator).Assembly;
        var prefix = $"{typeof(Resources.Subscription).Namespace}.";
        foreach (var name in assembly.GetManifestResourceNames().Where(n => n.StartsWith(prefix, StringComparison.Ordinal) && n.EndsWith(".yaml", StringComparison.Ordinal)).Order(StringComparer.Ordinal))
        {
            using var stream = assembly.GetManifestResourceStream(name)!;
            using var reader = new StreamReader(stream);
            var definition = Generate(reader.ReadToEnd());
            yield return ($"{definition["metadata"]!["name"]!.GetValue<string>()}.yaml", ToYaml(definition));
        }
    }

    /// <summary>
    /// Generates the CustomResourceDefinition of the specified resource definition
    /// </summary>
    /// <param name="definitionYaml">The YAML of the resource definition</param>
    /// <returns>The generated CustomResourceDefinition</returns>
    public static JsonObject Generate(string definitionYaml)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(definitionYaml);
        var definition = YamlSerializer.Default.Deserialize<JsonObject>(definitionYaml) ?? throw new FormatException("The resource definition is empty");
        var kind = definition["spec"]?["names"]?["kind"]?.GetValue<string>();
        foreach (var version in definition["spec"]?["versions"]?.AsArray().OfType<JsonObject>() ?? [])
        {
            if (version["schema"]?["openAPIV3Schema"] is JsonObject schema) MakeStructural(schema, false);
            if (kind != null && PrinterColumns.TryGetValue(kind, out var columns)) version["additionalPrinterColumns"] = columns.DeepClone();
        }
        return definition;
    }

    /// <summary>
    /// Rewrites the specified schema, and the schemas it contains, as structural schemas, which Kubernetes requires
    /// </summary>
    /// <param name="schema">The schema to rewrite</param>
    /// <param name="inJunctor">A boolean indicating whether the schema is nested in a logical junctor, where it may only declare value validations</param>
    /// <remarks>See https://kubernetes.io/docs/tasks/extend-kubernetes/custom-resources/custom-resource-definitions/#specifying-a-structural-schema</remarks>
    public static void MakeStructural(JsonObject schema, bool inJunctor)
    {
        ArgumentNullException.ThrowIfNull(schema);
        // a field of several types, such as an address or an object, cannot be described structurally: any value is kept, and the validators of Cloud Streams check it
        if (!inJunctor && IsPolymorphic(schema))
        {
            foreach (var keyword in (string[])["type", "properties", "items", "required", "additionalProperties", "format", "enum", .. Junctors]) schema.Remove(keyword);
            schema["x-kubernetes-preserve-unknown-fields"] = true;
            return;
        }
        if (schema["type"] is JsonArray types)
        {
            var nonNull = types.Select(t => t!.GetValue<string>()).Where(t => t != "null").ToList();
            schema["type"] = nonNull.Single();
            if (nonNull.Count != types.Count) schema["nullable"] = true;
        }
        if (schema["const"] is JsonNode constant)
        {
            schema.Remove("const");
            schema["enum"] = new JsonArray(constant.DeepClone());
        }
        ReplaceExclusiveBound(schema, "exclusiveMinimum", "minimum");
        ReplaceExclusiveBound(schema, "exclusiveMaximum", "maximum");
        if (inJunctor) foreach (var keyword in KeywordsForbiddenInJunctors) schema.Remove(keyword);
        // Kubernetes prunes the fields a schema does not declare: an object whose keys are free, such as attribute filters or labels, keeps them
        else if (schema["type"]?.GetValue<string>() == "object" && schema["properties"] == null && schema["additionalProperties"] == null) schema["x-kubernetes-preserve-unknown-fields"] = true;
        if (schema["properties"] is JsonObject properties) foreach (var (_, property) in properties) if (property is JsonObject propertySchema) MakeStructural(propertySchema, inJunctor);
        if (schema["items"] is JsonObject items) MakeStructural(items, inJunctor);
        if (schema["additionalProperties"] is JsonObject additionalProperties) MakeStructural(additionalProperties, inJunctor);
        foreach (var junctor in Junctors) if (schema[junctor] is JsonArray branches) foreach (var branch in branches.OfType<JsonObject>()) MakeStructural(branch, true);
        if (schema["not"] is JsonObject not) MakeStructural(not, true);
    }

    /// <summary>
    /// Determines whether the specified schema describes values of several types, either with a list of types, or with logical junctors whose branches declare
    /// different types
    /// </summary>
    static bool IsPolymorphic(JsonObject schema)
    {
        if (schema["type"] is JsonArray types) return types.Select(t => t!.GetValue<string>()).Count(t => t != "null") > 1;
        foreach (var junctor in Junctors)
        {
            if (schema[junctor] is not JsonArray branches) continue;
            var branchTypes = branches.OfType<JsonObject>().Select(b => b["type"]?.GetValue<string>()).Where(t => t != null).Distinct().ToList();
            if (branchTypes.Count > 1 || (branchTypes.Count == 1 && schema["type"] == null)) return true;
        }
        return false;
    }

    /// <summary>
    /// Replaces a numeric exclusive bound, as JSON Schema 2019-09 and later write it, with a bound and a boolean, as OpenAPI v3 does
    /// </summary>
    static void ReplaceExclusiveBound(JsonObject schema, string exclusiveKeyword, string boundKeyword)
    {
        if (schema[exclusiveKeyword] is not JsonValue value || value.TryGetValue<bool>(out _)) return;
        schema[boundKeyword] = value.DeepClone();
        schema[exclusiveKeyword] = true;
    }

    static JsonObject Column(string name, string type, string jsonPath, string? description = null)
    {
        var column = new JsonObject() { ["name"] = name, ["type"] = type, ["jsonPath"] = jsonPath };
        if (description != null) column["description"] = description;
        return column;
    }

    /// <summary>
    /// Writes the specified CustomResourceDefinition as YAML, preceded by a notice that it is generated
    /// </summary>
    /// <remarks>The keys are written as they are, which the serializer of Neuroglia would rename after its naming convention</remarks>
    static string ToYaml(JsonObject definition)
    {
        using var writer = new StringWriter();
        writer.Write("# Generated from the definitions of src/core/CloudStreams.Core/Resources by CustomResourceDefinitionGenerator. Do not edit: see #276\n");
        var emitter = new Emitter(writer);
        emitter.Emit(new StreamStart());
        emitter.Emit(new DocumentStart());
        Emit(emitter, definition);
        emitter.Emit(new DocumentEnd(true));
        emitter.Emit(new StreamEnd());
        return writer.ToString().ReplaceLineEndings("\n");
    }

    static void Emit(IEmitter emitter, JsonNode? node)
    {
        switch (node)
        {
            case JsonObject @object:
                emitter.Emit(new MappingStart(null, null, true, MappingStyle.Block));
                foreach (var (key, value) in @object)
                {
                    EmitString(emitter, key);
                    Emit(emitter, value);
                }
                emitter.Emit(new MappingEnd());
                break;
            case JsonArray array:
                // short lists of scalars, such as required properties or enumerations, are easier to read on one line
                var flow = array.Count <= 8 && array.All(i => i is JsonValue v && v.GetValueKind() is not JsonValueKind.String || i is JsonValue s && s.GetValue<string>().Length <= 24 && !s.GetValue<string>().Contains('\n'));
                emitter.Emit(new SequenceStart(null, null, true, flow ? SequenceStyle.Flow : SequenceStyle.Block));
                foreach (var item in array) Emit(emitter, item);
                emitter.Emit(new SequenceEnd());
                break;
            case JsonValue value when value.GetValueKind() == JsonValueKind.String:
                EmitString(emitter, value.GetValue<string>());
                break;
            case JsonValue value:
                // numbers and booleans as JSON writes them, e.g. 'true' rather than 'True'
                emitter.Emit(new Scalar(null, null, value.ToJsonString(), ScalarStyle.Plain, true, false));
                break;
            default:
                emitter.Emit(new Scalar(null, null, "null", ScalarStyle.Plain, true, false));
                break;
        }
    }

    /// <summary>
    /// Writes the specified string, quoted if it would otherwise be read as something else, such as a boolean, a number or a key
    /// </summary>
    static void EmitString(IEmitter emitter, string value)
    {
        var style = value.Contains('\n') ? ScalarStyle.Literal : IsPlainSafe(value) ? ScalarStyle.Plain : ScalarStyle.DoubleQuoted;
        emitter.Emit(new Scalar(null, null, value, style, true, true));
    }

    static bool IsPlainSafe(string value) => value.Length > 0
        && !char.IsWhiteSpace(value[0]) && !char.IsWhiteSpace(value[^1])
        && "-?:,[]{}#&*!|>'\"%@`.^$\\(".IndexOf(value[0]) < 0
        && !value.Contains(": ") && !value.Contains(" #") && !value.EndsWith(':')
        && !double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _)
        && value.ToLowerInvariant() is not ("true" or "false" or "null" or "yes" or "no" or "on" or "off" or "y" or "n" or "~");

}
