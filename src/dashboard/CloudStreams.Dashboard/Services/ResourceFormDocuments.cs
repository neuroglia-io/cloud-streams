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

using Neuroglia.Data;
using Neuroglia.Data.Infrastructure.ResourceOriented;
using Neuroglia.Serialization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CloudStreams.Dashboard.Services;

/// <summary>
/// Provides helpers to validate the names of resources
/// </summary>
public static partial class ResourceNames
{

    /// <summary>
    /// Gets the maximum length of the name of a resource
    /// </summary>
    public const int MaxLength = 63;

    /// <summary>
    /// Determines whether the specified name is a valid resource name: lower-case letters, digits and dashes, starting and ending with a letter or a digit
    /// </summary>
    /// <param name="name">The name to check</param>
    /// <returns>A boolean indicating whether the name is valid</returns>
    public static bool IsValid(string? name) => !string.IsNullOrEmpty(name) && name.Length <= MaxLength && NamePattern().IsMatch(name);

    [System.Text.RegularExpressions.GeneratedRegex("^[a-z0-9]([-a-z0-9]*[a-z0-9])?$")]
    private static partial System.Text.RegularExpressions.Regex NamePattern();

}

/// <summary>
/// Provides the conversions resource forms rely on: the form and the YAML view edit the same resource, and switching between them loses nothing (see #260)
/// </summary>
public static class ResourceFormDocuments
{

    /// <summary>
    /// Creates a deep copy of the specified resource, which a form edits without changing the original
    /// </summary>
    /// <typeparam name="TResource">The type of the resource to copy</typeparam>
    /// <param name="resource">The resource to copy</param>
    /// <param name="serializer">The service used to serialize JSON</param>
    /// <returns>A deep copy of the resource</returns>
    public static TResource Clone<TResource>(TResource resource, IJsonSerializer serializer)
        where TResource : class, IResource
    {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(serializer);
        return serializer.Deserialize<TResource>(serializer.SerializeToText(resource))!;
    }

    /// <summary>
    /// Formats the parts of the specified resource users own (its name, labels, annotations and spec) as YAML
    /// </summary>
    /// <param name="resource">The resource to format</param>
    /// <param name="jsonSerializer">The service used to serialize JSON</param>
    /// <param name="yamlSerializer">The service used to serialize YAML</param>
    /// <returns>The YAML document</returns>
    public static string ToYaml(IResource resource, IJsonSerializer jsonSerializer, IYamlSerializer yamlSerializer) => ResourceDocuments.ToYaml(ResourceDocuments.GetExportDocument(resource, jsonSerializer), yamlSerializer);

    /// <summary>
    /// Applies the specified YAML or JSON document, as edited in the YAML view, to the specified resource
    /// </summary>
    /// <typeparam name="TResource">The type of the resource</typeparam>
    /// <param name="resource">The resource to apply the document to</param>
    /// <param name="text">The YAML or JSON document</param>
    /// <param name="canRename">A boolean indicating whether the name may change, which it may only before the resource is created</param>
    /// <param name="jsonSerializer">The service used to serialize JSON</param>
    /// <param name="yamlSerializer">The service used to serialize YAML</param>
    /// <returns>A new resource, made of the specified one and of the name, labels, annotations and spec of the document</returns>
    /// <exception cref="FormatException">The document is not a valid resource of the expected kind</exception>
    /// <remarks>What users do not own, such as the status, the generation or the resource version, is kept from the specified resource</remarks>
    public static TResource Apply<TResource>(TResource resource, string text, bool canRename, IJsonSerializer jsonSerializer, IYamlSerializer yamlSerializer)
        where TResource : class, IResource
    {
        ArgumentNullException.ThrowIfNull(resource);
        var documents = ResourceDocuments.ParseDocuments(text ?? string.Empty, yamlSerializer);
        if (documents.Count != 1) throw new FormatException("The document must describe exactly one resource");
        var document = documents[0];
        if (document["kind"]?.GetValue<string>() is { Length: > 0 } kind && !string.Equals(kind, resource.Kind, StringComparison.OrdinalIgnoreCase)) throw new FormatException($"The document describes a '{kind}', not a '{resource.Kind}'");
        var node = JsonNode.Parse(jsonSerializer.SerializeToText(resource))!.AsObject();
        var metadata = node["metadata"] as JsonObject ?? [];
        node["metadata"] = metadata;
        var editedMetadata = document["metadata"] as JsonObject;
        if (editedMetadata?["name"]?.GetValue<string>() is { Length: > 0 } name && name != resource.GetName())
        {
            if (!canRename) throw new FormatException($"The name of an existing resource cannot change: it is '{resource.GetName()}'");
            metadata["name"] = name;
        }
        foreach (var property in new[] { "labels", "annotations" })
        {
            if (editedMetadata?[property] is JsonNode value) metadata[property] = value.DeepClone();
            else metadata.Remove(property);
        }
        node["spec"] = document["spec"]?.DeepClone() ?? throw new FormatException("The document must have a 'spec'");
        try { return jsonSerializer.Deserialize<TResource>(node.ToJsonString()) ?? throw new FormatException("The document is not a valid resource"); }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException) { throw new FormatException($"The document is not a valid resource: {ex.Message}", ex); }
    }

    /// <summary>
    /// Computes the changes between the parts users own of the specified resources, as shown before they are saved
    /// </summary>
    /// <param name="original">The resource as it is saved</param>
    /// <param name="updated">The resource as edited</param>
    /// <param name="jsonSerializer">The service used to serialize JSON</param>
    /// <param name="yamlSerializer">The service used to serialize YAML</param>
    /// <returns>The lines of the diff</returns>
    public static IReadOnlyList<TextDiffLine> Diff(IResource original, IResource updated, IJsonSerializer jsonSerializer, IYamlSerializer yamlSerializer) => TextDiff.Compute(
        ResourceDocuments.ToYaml(ResourceDocuments.GetEditableDocument(original, jsonSerializer), yamlSerializer),
        ResourceDocuments.ToYaml(ResourceDocuments.GetEditableDocument(updated, jsonSerializer), yamlSerializer));

    /// <summary>
    /// Computes the JSON patch that turns the parts users own of the original resource into the updated ones
    /// </summary>
    /// <param name="original">The resource as it is saved</param>
    /// <param name="updated">The resource as edited</param>
    /// <param name="jsonSerializer">The service used to serialize JSON</param>
    /// <returns>The patch, or null if nothing changed</returns>
    public static Patch? GetPatch(IResource original, IResource updated, IJsonSerializer jsonSerializer)
    {
        var patch = JsonCons.Utilities.JsonPatch.FromDiff(
            JsonDocument.Parse(ResourceDocuments.GetEditableDocument(original, jsonSerializer).ToJsonString()).RootElement,
            JsonDocument.Parse(ResourceDocuments.GetEditableDocument(updated, jsonSerializer).ToJsonString()).RootElement);
        return patch.RootElement.GetArrayLength() < 1 ? null : new Patch(PatchType.JsonPatch, patch);
    }

    /// <summary>
    /// Converts the specified duration into an amount of the largest unit it is a whole number of, as shown by duration inputs
    /// </summary>
    /// <param name="duration">The duration to convert</param>
    /// <returns>The amount, and its unit: 'ms', 's', 'min', 'h' or 'd'</returns>
    public static (double Amount, string Unit) ToAmount(TimeSpan duration)
    {
        if (duration == TimeSpan.Zero) return (0, "s");
        if (duration.Ticks % TimeSpan.TicksPerDay == 0) return (duration.TotalDays, "d");
        if (duration.Ticks % TimeSpan.TicksPerHour == 0) return (duration.TotalHours, "h");
        if (duration.Ticks % TimeSpan.TicksPerMinute == 0) return (duration.TotalMinutes, "min");
        if (duration.Ticks % TimeSpan.TicksPerSecond == 0) return (duration.TotalSeconds, "s");
        return (duration.TotalMilliseconds, "ms");
    }

    /// <summary>
    /// Converts the specified amount of the specified unit into a duration
    /// </summary>
    /// <param name="amount">The amount</param>
    /// <param name="unit">The unit: 'ms', 's', 'min', 'h' or 'd'</param>
    /// <returns>The duration</returns>
    public static TimeSpan FromAmount(double amount, string unit) => unit switch
    {
        "ms" => TimeSpan.FromMilliseconds(amount),
        "min" => TimeSpan.FromMinutes(amount),
        "h" => TimeSpan.FromHours(amount),
        "d" => TimeSpan.FromDays(amount),
        _ => TimeSpan.FromSeconds(amount)
    };

}
