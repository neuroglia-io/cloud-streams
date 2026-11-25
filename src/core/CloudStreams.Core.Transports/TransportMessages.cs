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

using Neuroglia.Eventing.CloudEvents;
using Neuroglia.Serialization;
using System.Text;

namespace CloudStreams.Core.Transports;

/// <summary>
/// Represents a message consumed from a channel, whose headers are normalized to those of the CloudEvents HTTP binding, so that it is decoded like an HTTP request
/// </summary>
/// <param name="ContentType">The content type of the message, if any</param>
/// <param name="Headers">The headers of the message. The attributes of a cloud event in the binary content mode are prefixed with <c>ce-</c>, whatever the transport</param>
/// <param name="Body">The body of the message</param>
/// <param name="Description">A description of the message, such as its topic, partition and offset, used in logs</param>
public sealed record TransportMessage(string? ContentType, IReadOnlyList<KeyValuePair<string, string?>> Headers, ReadOnlyMemory<byte> Body, string Description);

/// <summary>
/// Encodes and decodes the cloud events carried by messages, in the structured content mode when encoding, and in either mode when decoding (see #218)
/// </summary>
/// <remarks>
/// Each transport's binding prefixes the attributes of the binary content mode differently: <c>ce_</c> with Kafka, <c>cloudEvents:</c> or <c>cloudEvents_</c> with
/// AMQP, <c>ce-</c> with NATS and HTTP. Messages are always sent in the structured content mode, which every binding supports, and which carries extension attributes
/// whatever their type
/// </remarks>
public static class TransportMessageEncoding
{

    /// <summary>
    /// Gets the name of the header that conveys the content type, with transports whose messages have no content type of their own
    /// </summary>
    public const string ContentTypeHeader = "content-type";

    const string HttpPrefix = "ce-";

    static readonly string[] Prefixes = ["ce_", "ce-", "cloudEvents:", "cloudEvents_"];

    /// <summary>
    /// Encodes the specified cloud event in the structured content mode
    /// </summary>
    /// <param name="e">The cloud event to encode</param>
    /// <param name="serializer">The service used to serialize the cloud event</param>
    /// <returns>The body of the message</returns>
    public static byte[] Encode(CloudEvent e, IJsonSerializer serializer)
    {
        ArgumentNullException.ThrowIfNull(e);
        ArgumentNullException.ThrowIfNull(serializer);
        return Encoding.UTF8.GetBytes(serializer.SerializeToText(e));
    }

    /// <summary>
    /// Gets the key that orders the specified cloud event among the others: its <c>partitionkey</c>, as the CloudEvents Kafka binding requires, or its subject
    /// </summary>
    /// <param name="e">The cloud event to get the key of</param>
    /// <returns>The key of the cloud event, if any</returns>
    public static string? GetKey(CloudEvent e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (e.TryGetAttribute("partitionkey", out var key) && key?.ToString() is { Length: > 0 } partitionKey) return partitionKey;
        return string.IsNullOrWhiteSpace(e.Subject) ? null : e.Subject;
    }

    /// <summary>
    /// Normalizes the name of the specified header to that of the CloudEvents HTTP binding, i.e. <c>ce_type</c> or <c>cloudEvents:type</c> to <c>ce-type</c>
    /// </summary>
    /// <param name="name">The name of the header</param>
    /// <returns>The normalized name</returns>
    public static string NormalizeHeader(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        foreach (var prefix in Prefixes)
        {
            if (name.Length > prefix.Length && name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return HttpPrefix + name[prefix.Length..].ToLowerInvariant();
        }
        return name;
    }

    /// <summary>
    /// Decodes the specified header value, which transports carry as bytes, or as strings
    /// </summary>
    /// <param name="value">The value to decode</param>
    /// <returns>The decoded value</returns>
    public static string? DecodeHeaderValue(object? value) => value switch
    {
        null => null,
        byte[] bytes => Encoding.UTF8.GetString(bytes),
        ReadOnlyMemory<byte> memory => Encoding.UTF8.GetString(memory.Span),
        _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)
    };

}
