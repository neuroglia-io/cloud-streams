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
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CloudStreams.Gateway.Application.Services;

/// <summary>
/// Enumerates the content modes of the CloudEvents HTTP protocol binding
/// </summary>
public enum CloudEventContentMode
{
    /// <summary>
    /// Indicates that the request's content type is not supported
    /// </summary>
    Unsupported,
    /// <summary>
    /// Indicates that the request's body is a cloud event, formatted as <c>application/cloudevents+json</c>
    /// </summary>
    Structured,
    /// <summary>
    /// Indicates that the request's body is an array of cloud events, formatted as <c>application/cloudevents-batch+json</c>
    /// </summary>
    Batch,
    /// <summary>
    /// Indicates that the request's body is the data of a cloud event, whose context attributes are carried by <c>ce-</c> prefixed headers
    /// </summary>
    Binary
}

/// <summary>
/// Implements the CloudEvents HTTP protocol binding: reads cloud events in the structured, batched and binary content modes (see #208)
/// </summary>
/// <remarks>See <see href="https://github.com/cloudevents/spec/blob/main/cloudevents/bindings/http-protocol-binding.md"/></remarks>
public static class CloudEventHttpBinding
{

    /// <summary>
    /// Gets the media type of batches of cloud events
    /// </summary>
    public const string BatchContentType = "application/cloudevents-batch+json";

    /// <summary>
    /// Gets the prefix of the headers that carry the context attributes of cloud events in the binary content mode
    /// </summary>
    public const string HeaderPrefix = "ce-";

    /// <summary>
    /// Gets the header that identifies the binary content mode
    /// </summary>
    public const string SpecVersionHeader = HeaderPrefix + CloudEventAttributes.SpecVersion;

    /// <summary>
    /// Determines the content mode of a request
    /// </summary>
    /// <param name="contentType">The request's content type, if any</param>
    /// <param name="hasSpecVersionHeader">A boolean indicating whether or not the request defines the <c>ce-specversion</c> header</param>
    /// <returns>The request's <see cref="CloudEventContentMode"/></returns>
    public static CloudEventContentMode GetContentMode(string? contentType, bool hasSpecVersionHeader)
    {
        var mediaType = GetMediaType(contentType);
        if (string.Equals(mediaType, CloudEventContentType.Json, StringComparison.OrdinalIgnoreCase)) return CloudEventContentMode.Structured;
        if (string.Equals(mediaType, BatchContentType, StringComparison.OrdinalIgnoreCase)) return CloudEventContentMode.Batch;
        // a structured or batched event of another format, e.g. application/cloudevents+xml, is not supported, whatever its headers
        if (mediaType?.StartsWith(CloudEventContentType.Prefix, StringComparison.OrdinalIgnoreCase) == true) return CloudEventContentMode.Unsupported;
        return hasSpecVersionHeader ? CloudEventContentMode.Binary : CloudEventContentMode.Unsupported;
    }

    /// <summary>
    /// Reads a cloud event in the structured content mode
    /// </summary>
    /// <param name="body">The request's body</param>
    /// <param name="serializer">The service used to deserialize cloud events</param>
    /// <returns>The cloud event</returns>
    /// <exception cref="FormatException">The body is not a cloud event</exception>
    public static CloudEvent ReadStructured(ReadOnlyMemory<byte> body, IJsonSerializer serializer)
    {
        ArgumentNullException.ThrowIfNull(serializer);
        return ReadEvent(ParseJson(body, "The request's body"), serializer, "The request's body");
    }

    /// <summary>
    /// Reads the elements of a batch of cloud events, which are only deserialized individually, by <see cref="ReadEvent"/>, so that an invalid element does not
    /// prevent the others from being ingested
    /// </summary>
    /// <param name="body">The request's body</param>
    /// <returns>The elements of the batch</returns>
    /// <exception cref="FormatException">The body is not a JSON array</exception>
    public static IReadOnlyList<JsonNode?> ReadBatch(ReadOnlyMemory<byte> body)
    {
        if (ParseJson(body, "The request's body") is not JsonArray batch) throw new FormatException("A batch of cloud events must be a JSON array");
        return [.. batch];
    }

    /// <summary>
    /// Reads the specified JSON node as a cloud event
    /// </summary>
    /// <param name="node">The JSON node to read</param>
    /// <param name="serializer">The service used to deserialize cloud events</param>
    /// <param name="description">A description of the node, used in error messages</param>
    /// <returns>The cloud event</returns>
    /// <exception cref="FormatException">The node is not a cloud event</exception>
    public static CloudEvent ReadEvent(JsonNode? node, IJsonSerializer serializer, string description = "The cloud event")
    {
        ArgumentNullException.ThrowIfNull(serializer);
        if (node is not JsonObject) throw new FormatException($"{description} must be a JSON object");
        try
        {
            return serializer.Deserialize<CloudEvent>(node) ?? throw new FormatException($"{description} must be a JSON object");
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException or ArgumentException)
        {
            throw new FormatException($"{description} is not a valid cloud event: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Reads a cloud event in the binary content mode
    /// </summary>
    /// <param name="contentType">The request's content type, which is the cloud event's <c>datacontenttype</c>, if any</param>
    /// <param name="headers">The request's headers</param>
    /// <param name="body">The request's body, which is the cloud event's data</param>
    /// <param name="serializer">The service used to deserialize cloud events</param>
    /// <returns>The cloud event</returns>
    /// <exception cref="FormatException">The headers or the body do not describe a valid cloud event</exception>
    /// <remarks>
    /// The data of a JSON media type is read as JSON, the data of a text media type as a string, and any other data as binary, i.e. <c>data_base64</c>
    /// </remarks>
    public static CloudEvent ReadBinary(string? contentType, IEnumerable<KeyValuePair<string, string?>> headers, ReadOnlyMemory<byte> body, IJsonSerializer serializer)
    {
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(serializer);
        var e = new JsonObject();
        foreach (var (name, value) in headers)
        {
            if (!name.StartsWith(HeaderPrefix, StringComparison.OrdinalIgnoreCase) || name.Length == HeaderPrefix.Length || value == null) continue;
            var attribute = name[HeaderPrefix.Length..].ToLowerInvariant();
            // 'data' and 'datacontenttype' are carried by the body and its content type
            if (attribute is CloudEventAttributes.Data or CloudEventAttributes.DataContentType) continue;
            // header values are percent-encoded when they contain characters headers cannot carry
            string decoded;
            try { decoded = Uri.UnescapeDataString(value); }
            catch (UriFormatException ex) { throw new FormatException($"The value of the '{name}' header is not correctly percent-encoded", ex); }
            e[attribute] = decoded;
        }
        if (!string.IsNullOrWhiteSpace(contentType)) e[CloudEventAttributes.DataContentType] = contentType;
        if (!body.IsEmpty)
        {
            var mediaType = GetMediaType(contentType);
            if (IsJson(mediaType)) e[CloudEventAttributes.Data] = ParseJson(body, "The cloud event's data");
            else if (IsText(mediaType)) e[CloudEventAttributes.Data] = GetEncoding(contentType).GetString(body.Span);
            else e[CloudEventAttributes.DataBase64] = Convert.ToBase64String(body.Span);
        }
        return ReadEvent(e, serializer, "The request's headers");
    }

    static JsonNode? ParseJson(ReadOnlyMemory<byte> body, string description)
    {
        try { return JsonNode.Parse(body.Span); }
        catch (JsonException ex) { throw new FormatException($"{description} is not valid JSON: {ex.Message}", ex); }
    }

    static string? GetMediaType(string? contentType) => MediaTypeHeaderValue.TryParse(contentType, out var value) ? value.MediaType : null;

    static bool IsJson(string? mediaType) => mediaType != null
        && (mediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase) || mediaType.Equals("text/json", StringComparison.OrdinalIgnoreCase) || mediaType.EndsWith("+json", StringComparison.OrdinalIgnoreCase));

    static bool IsText(string? mediaType) => mediaType != null
        && (mediaType.StartsWith("text/", StringComparison.OrdinalIgnoreCase) || mediaType.Equals("application/xml", StringComparison.OrdinalIgnoreCase) || mediaType.EndsWith("+xml", StringComparison.OrdinalIgnoreCase));

    static Encoding GetEncoding(string? contentType)
    {
        if (!MediaTypeHeaderValue.TryParse(contentType, out var value) || string.IsNullOrWhiteSpace(value.CharSet)) return Encoding.UTF8;
        try { return Encoding.GetEncoding(value.CharSet.Trim('"')); }
        catch (ArgumentException) { return Encoding.UTF8; }
    }

}
