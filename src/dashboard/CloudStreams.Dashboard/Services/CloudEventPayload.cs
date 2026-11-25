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

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace CloudStreams.Dashboard.Services;

/// <summary>
/// Enumerates the kinds of payloads a cloud event may carry
/// </summary>
public enum CloudEventPayloadKind
{
    /// <summary>
    /// Indicates that the cloud event carries no data
    /// </summary>
    None,
    /// <summary>
    /// Indicates that the cloud event's data is JSON
    /// </summary>
    Json,
    /// <summary>
    /// Indicates that the cloud event's data is text of another media type
    /// </summary>
    Text,
    /// <summary>
    /// Indicates that the cloud event's data is binary, carried by its <c>data_base64</c> attribute
    /// </summary>
    Binary
}

/// <summary>
/// Describes the payload of a cloud event, as the dashboard displays, copies and downloads it (see #228)
/// </summary>
/// <param name="Kind">The kind of the payload</param>
/// <param name="Display">The payload as it is displayed: indented JSON, text, or a hexadecimal preview of binary data</param>
/// <param name="Content">The payload's bytes, as they are downloaded</param>
/// <param name="ContentType">The payload's media type</param>
/// <param name="FileExtension">The extension of the file the payload is downloaded as</param>
public sealed record CloudEventPayload(CloudEventPayloadKind Kind, string Display, byte[] Content, string ContentType, string FileExtension)
{

    /// <summary>
    /// Gets the maximum amount of bytes previewed for binary payloads
    /// </summary>
    public const int BinaryPreviewLength = 256;

    static readonly JsonSerializerOptions IndentedOptions = new() { WriteIndented = true };

    /// <summary>
    /// Describes the payload of the specified cloud event
    /// </summary>
    /// <param name="e">The cloud event to describe the payload of</param>
    /// <returns>A new <see cref="CloudEventPayload"/></returns>
    public static CloudEventPayload From(CloudEvent e)
    {
        ArgumentNullException.ThrowIfNull(e);
        var mediaType = MediaTypeHeaderValue.TryParse(e.DataContentType, out var parsed) ? parsed.MediaType : null;
        if (!string.IsNullOrEmpty(e.DataBase64))
        {
            byte[] bytes;
            try { bytes = Convert.FromBase64String(e.DataBase64); }
            catch (FormatException) { return new(CloudEventPayloadKind.Text, e.DataBase64, Encoding.UTF8.GetBytes(e.DataBase64), "text/plain", ".txt"); }
            var preview = Convert.ToHexString(bytes, 0, Math.Min(bytes.Length, BinaryPreviewLength));
            var display = $"{bytes.Length} bytes{Environment.NewLine}{string.Join(' ', Enumerable.Range(0, preview.Length / 2).Select(i => preview.Substring(i * 2, 2)))}{(bytes.Length > BinaryPreviewLength ? " …" : string.Empty)}";
            return new(CloudEventPayloadKind.Binary, display, bytes, mediaType ?? "application/octet-stream", ExtensionOf(mediaType, ".bin"));
        }
        if (e.Data == null || e.Data is JsonElement { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined }) return new(CloudEventPayloadKind.None, string.Empty, [], mediaType ?? "application/json", ".json");
        var text = e.Data switch
        {
            string s => s,
            JsonElement { ValueKind: JsonValueKind.String } element => element.GetString(),
            _ => null
        };
        if (text != null && !IsJson(mediaType)) return new(CloudEventPayloadKind.Text, text, Encoding.UTF8.GetBytes(text), mediaType ?? "text/plain", ExtensionOf(mediaType, ".txt"));
        var json = JsonSerializer.Serialize(e.Data, IndentedOptions);
        return new(CloudEventPayloadKind.Json, json, Encoding.UTF8.GetBytes(json), mediaType ?? "application/json", ".json");
    }

    static bool IsJson(string? mediaType) => mediaType == null || mediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase) || mediaType.Equals("text/json", StringComparison.OrdinalIgnoreCase) || mediaType.EndsWith("+json", StringComparison.OrdinalIgnoreCase);

    static string ExtensionOf(string? mediaType, string fallback) => mediaType?.ToLowerInvariant() switch
    {
        "application/xml" or "text/xml" => ".xml",
        "text/csv" => ".csv",
        "text/html" => ".html",
        "text/plain" => ".txt",
        "application/pdf" => ".pdf",
        "image/png" => ".png",
        "image/jpeg" => ".jpg",
        "application/x-protobuf" or "application/protobuf" => ".pb",
        "application/avro" => ".avro",
        _ => fallback
    };

}

/// <summary>
/// Defines extensions for the cloud events the dashboard displays
/// </summary>
public static class DashboardCloudEventExtensions
{

    /// <summary>
    /// Gets the sequence of the specified cloud event, which is its offset in the stream or partition it has been read from, if any
    /// </summary>
    /// <param name="e">The cloud event to get the sequence of</param>
    /// <param name="sequence">The sequence of the cloud event</param>
    /// <returns>A boolean indicating whether or not the cloud event has a sequence</returns>
    public static bool TryGetSequence(this CloudEvent e, out ulong sequence)
    {
        sequence = 0;
        return e.ExtensionAttributes != null && e.ExtensionAttributes.TryGetValue(CloudEventExtensionAttributes.Sequence, out var value) && ulong.TryParse(value?.ToString(), out sequence);
    }

    /// <summary>
    /// Converts the specified record into the cloud event it describes, as the gateway does
    /// </summary>
    /// <param name="record">The record to convert</param>
    /// <param name="serializer">The service used to convert the record</param>
    /// <returns>The cloud event the record describes</returns>
    /// <remarks>Binary data is stored with the context attributes, as <c>data_base64</c>, which is not a valid attribute name and is therefore restored explicitly</remarks>
    public static CloudEvent ToCloudEvent(this CloudEventRecord record, Neuroglia.Serialization.IJsonSerializer serializer)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(serializer);
        var attributes = record.Metadata.ContextAttributes;
        var e = new Dictionary<string, object?>(attributes.Where(a => a.Key.Length > 0 && a.Key.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c))).Select(a => new KeyValuePair<string, object?>(a.Key, a.Value)));
        var hasData = record.Data != null && record.Data is not JsonElement { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined };
        if (!hasData && attributes.TryGetValue(CloudEventAttributes.DataBase64, out var base64) && base64?.ToString() is { Length: > 0 } text) e[CloudEventAttributes.DataBase64] = text;
        else e[CloudEventAttributes.Data] = record.Data;
        return serializer.Deserialize<CloudEvent>(serializer.SerializeToText(e))!;
    }

}
