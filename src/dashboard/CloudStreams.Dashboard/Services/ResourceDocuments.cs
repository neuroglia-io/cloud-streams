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

using Neuroglia.Serialization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CloudStreams.Dashboard.Services;

/// <summary>
/// Converts resources to and from the documents users edit, export and import (see #227)
/// </summary>
/// <remarks>
/// Users only own the specification, labels and annotations of a resource: the rest of its metadata is managed by the server, and its status by the controllers. Exported
/// documents, and the documents compared before saving, are therefore made of those alone
/// </remarks>
public static class ResourceDocuments
{

    static readonly JsonSerializerOptions IndentedOptions = new() { WriteIndented = true };

    /// <summary>
    /// Gets the parts of the specified resource users may edit: its spec, labels and annotations
    /// </summary>
    /// <param name="resource">The resource to get the editable document of</param>
    /// <param name="serializer">The service used to serialize the resource</param>
    /// <returns>A new <see cref="JsonObject"/> of the form <c>{ "metadata": { "labels": ..., "annotations": ... }, "spec": ... }</c></returns>
    /// <remarks>'metadata' is always present, so that a patch computed from two documents targets '/metadata/labels' or '/metadata/annotations', and never the whole metadata</remarks>
    public static JsonObject GetEditableDocument(object? resource, IJsonSerializer serializer)
    {
        ArgumentNullException.ThrowIfNull(serializer);
        var node = resource == null ? null : JsonNode.Parse(serializer.SerializeToText(resource)) as JsonObject;
        var metadata = new JsonObject();
        var document = new JsonObject() { ["metadata"] = metadata };
        if (node?["metadata"] is JsonObject source)
        {
            foreach (var property in new[] { "labels", "annotations" }) if (source[property] is JsonNode value) metadata[property] = value.DeepClone();
        }
        if (node?["spec"] is JsonNode spec) document["spec"] = spec.DeepClone();
        return document;
    }

    /// <summary>
    /// Gets the document the specified resource is exported as: its api version, kind, name, labels, annotations and spec
    /// </summary>
    /// <param name="resource">The resource to export</param>
    /// <param name="serializer">The service used to serialize the resource</param>
    /// <returns>A new <see cref="JsonObject"/></returns>
    public static JsonObject GetExportDocument(IResource resource, IJsonSerializer serializer)
    {
        ArgumentNullException.ThrowIfNull(resource);
        var editable = GetEditableDocument(resource, serializer);
        var metadata = new JsonObject() { ["name"] = resource.GetName() };
        foreach (var (key, value) in editable["metadata"]!.AsObject()) metadata[key] = value?.DeepClone();
        var document = new JsonObject()
        {
            ["apiVersion"] = resource.ApiVersion,
            ["kind"] = resource.Kind,
            ["metadata"] = metadata
        };
        if (editable["spec"] is JsonNode spec) document["spec"] = spec.DeepClone();
        return document;
    }

    /// <summary>
    /// Exports the specified resources as a YAML stream: one document per resource, separated by <c>---</c>
    /// </summary>
    /// <param name="resources">The resources to export</param>
    /// <param name="jsonSerializer">The service used to serialize the resources</param>
    /// <param name="yamlSerializer">The service used to convert them to YAML</param>
    /// <returns>The YAML stream</returns>
    public static string ExportYaml(IEnumerable<IResource> resources, IJsonSerializer jsonSerializer, IYamlSerializer yamlSerializer)
    {
        ArgumentNullException.ThrowIfNull(resources);
        var builder = new StringBuilder();
        foreach (var resource in resources.OrderBy(r => r.GetName(), StringComparer.Ordinal))
        {
            if (builder.Length > 0) builder.AppendLine("---");
            builder.Append(yamlSerializer.ConvertFromJson(GetExportDocument(resource, jsonSerializer).ToJsonString()).TrimEnd()).AppendLine();
        }
        return builder.ToString();
    }

    /// <summary>
    /// Parses the specified text, a JSON object, a JSON array of objects, or a YAML stream of one or more documents, into the documents it holds
    /// </summary>
    /// <param name="text">The text to parse</param>
    /// <param name="yamlSerializer">The service used to convert YAML documents to JSON</param>
    /// <returns>The documents the text holds</returns>
    /// <exception cref="FormatException">The text, or one of its documents, is not valid, or holds something else than objects</exception>
    public static IReadOnlyList<JsonObject> ParseDocuments(string text, IYamlSerializer yamlSerializer)
    {
        ArgumentNullException.ThrowIfNull(text);
        var trimmed = text.TrimStart('﻿', ' ', '\t', '\r', '\n');
        if (trimmed.StartsWith('{') || trimmed.StartsWith('['))
        {
            JsonNode? node;
            try { node = JsonNode.Parse(trimmed); }
            catch (JsonException ex) { throw new FormatException($"The JSON is not valid: {ex.Message}", ex); }
            return node switch
            {
                JsonObject o => [o],
                JsonArray a when a.All(i => i is JsonObject) => [.. a.Select(i => i!.AsObject())],
                _ => throw new FormatException("The JSON must be an object, or an array of objects")
            };
        }
        var documents = new List<JsonObject>();
        var index = 0;
        foreach (var yaml in SplitYamlStream(trimmed))
        {
            index++;
            if (string.IsNullOrWhiteSpace(yaml) || yaml.Split('\n').All(l => string.IsNullOrWhiteSpace(l) || l.TrimStart().StartsWith('#'))) continue;
            JsonNode? node;
            try { node = JsonNode.Parse(yamlSerializer.ConvertToJson(yaml)); }
            catch (Exception ex) { throw new FormatException($"The YAML document #{index} is not valid: {ex.Message}", ex); }
            if (node is not JsonObject document) throw new FormatException($"The YAML document #{index} is not an object");
            documents.Add(document);
        }
        return documents;
    }

    static IEnumerable<string> SplitYamlStream(string text)
    {
        var current = new StringBuilder();
        foreach (var line in text.Replace("\r\n", "\n").Split('\n'))
        {
            if (line.TrimEnd() == "---")
            {
                yield return current.ToString();
                current.Clear();
                continue;
            }
            if (line.TrimEnd() == "...") continue;
            current.Append(line).Append('\n');
        }
        yield return current.ToString();
    }

    /// <summary>
    /// Formats the specified document as indented YAML, for display
    /// </summary>
    /// <param name="document">The document to format</param>
    /// <param name="yamlSerializer">The service used to convert the document to YAML</param>
    /// <returns>The document, as YAML</returns>
    public static string ToYaml(JsonNode document, IYamlSerializer yamlSerializer) => yamlSerializer.ConvertFromJson(document.ToJsonString()).TrimEnd() + "\n";

    /// <summary>
    /// Formats the specified document as indented JSON, for display
    /// </summary>
    /// <param name="document">The document to format</param>
    /// <returns>The document, as indented JSON</returns>
    public static string ToJson(JsonNode document) => document.ToJsonString(IndentedOptions);

}

/// <summary>
/// Enumerates the kinds of lines of a <see cref="TextDiff"/>
/// </summary>
public enum TextDiffLineKind
{
    /// <summary>
    /// Indicates a line both texts share
    /// </summary>
    Unchanged,
    /// <summary>
    /// Indicates a line only the original text has
    /// </summary>
    Removed,
    /// <summary>
    /// Indicates a line only the modified text has
    /// </summary>
    Added
}

/// <summary>
/// Describes a line of a <see cref="TextDiff"/>
/// </summary>
/// <param name="Kind">The kind of the line</param>
/// <param name="Text">The text of the line</param>
public sealed record TextDiffLine(TextDiffLineKind Kind, string Text);

/// <summary>
/// Computes the line-by-line differences between two texts, which the resource editor shows before saving changes (see #227)
/// </summary>
public static class TextDiff
{

    /// <summary>
    /// Computes the differences between the specified texts, as the lines of the longest common subsequence and the lines removed and added around them
    /// </summary>
    /// <param name="original">The original text</param>
    /// <param name="modified">The modified text</param>
    /// <returns>The lines of the differences, in order</returns>
    public static IReadOnlyList<TextDiffLine> Compute(string original, string modified)
    {
        var a = Lines(original);
        var b = Lines(modified);
        // resources are small: a quadratic longest common subsequence is fine
        var lengths = new int[a.Length + 1, b.Length + 1];
        for (var i = a.Length - 1; i >= 0; i--)
            for (var j = b.Length - 1; j >= 0; j--)
                lengths[i, j] = a[i] == b[j] ? lengths[i + 1, j + 1] + 1 : Math.Max(lengths[i + 1, j], lengths[i, j + 1]);
        var lines = new List<TextDiffLine>();
        int x = 0, y = 0;
        while (x < a.Length && y < b.Length)
        {
            if (a[x] == b[y]) { lines.Add(new(TextDiffLineKind.Unchanged, a[x])); x++; y++; }
            else if (lengths[x + 1, y] >= lengths[x, y + 1]) lines.Add(new(TextDiffLineKind.Removed, a[x++]));
            else lines.Add(new(TextDiffLineKind.Added, b[y++]));
        }
        while (x < a.Length) lines.Add(new(TextDiffLineKind.Removed, a[x++]));
        while (y < b.Length) lines.Add(new(TextDiffLineKind.Added, b[y++]));
        return lines;
    }

    static string[] Lines(string text) => string.IsNullOrEmpty(text) ? [] : text.Replace("\r\n", "\n").TrimEnd('\n').Split('\n');

}
