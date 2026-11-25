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
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace CloudStreams.Core;

/// <summary>
/// Defines extensions for <see cref="CloudEvent"/>s
/// </summary>
public static class CloudEventExtensions
{

    /// <summary>
    /// Gets the <see cref="CloudEvent"/>'s sequence
    /// </summary>
    /// <param name="e">The <see cref="CloudEvent"/> to get the sequence for</param>
    /// <returns>The <see cref="CloudEvent"/>'s sequence</returns>
    public static ulong? GetSequence(this CloudEvent e)
    {
        if (!e.TryGetAttribute(CloudEventExtensionAttributes.Sequence, out var value) || value == null) return null;
        return value switch
        {
            string str => ulong.TryParse(str, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) ? parsed : null,
            ulong num => num,
            long num => num >= 0 ? (ulong)num : null,
            int num => num >= 0 ? (ulong)num : null,
            uint num => num,
            decimal num => num >= 0 && num <= ulong.MaxValue && decimal.Truncate(num) == num ? (ulong)num : null,
            JsonElement { ValueKind: JsonValueKind.Number } jsonElem => jsonElem.TryGetUInt64(out var number) ? number : null,
            JsonElement { ValueKind: JsonValueKind.String } jsonElem => ulong.TryParse(jsonElem.GetString(), NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) ? parsed : null,
            _ => null
        };
    }

    /// <summary>
    /// Converts the <see cref="CloudEvent"/> into a new <see cref="HttpContent"/>
    /// </summary>
    /// <param name="e">The <see cref="CloudEvent"/> to convert to a new <see cref="HttpContent"/></param>
    /// <param name="serializer">The service used to serialize the <see cref="CloudEvent"/> to a new <see cref="HttpContent"/></param>
    /// <returns>The <see cref="CloudEvent"/>'s <see cref="HttpContent"/> representation</returns>
    public static HttpContent ToHttpContent(this CloudEvent e, IJsonSerializer? serializer = null)
    {
        serializer ??= Neuroglia.Serialization.Json.JsonSerializer.Default;
        return new StringContent(serializer.SerializeToText(e), Encoding.UTF8, CloudEventContentType.Json);
    }

    /// <summary>
    /// Gets the media type of batches of cloud events, as defined by the CloudEvents HTTP binding
    /// </summary>
    public const string BatchContentType = "application/cloudevents-batch+json";

    /// <summary>
    /// Converts the specified <see cref="CloudEvent"/>s into a new <see cref="HttpContent"/> that describes them as a batch, formatted as <c>application/cloudevents-batch+json</c>
    /// </summary>
    /// <param name="events">The <see cref="CloudEvent"/>s to convert</param>
    /// <param name="serializer">The service used to serialize the <see cref="CloudEvent"/>s</param>
    /// <returns>A new <see cref="HttpContent"/> that describes the batch</returns>
    public static HttpContent ToBatchHttpContent(this IEnumerable<CloudEvent> events, IJsonSerializer? serializer = null)
    {
        ArgumentNullException.ThrowIfNull(events);
        serializer ??= Neuroglia.Serialization.Json.JsonSerializer.Default;
        return new StringContent(serializer.SerializeToText(events.ToList()), Encoding.UTF8, BatchContentType);
    }

}
