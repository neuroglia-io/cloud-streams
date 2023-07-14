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

using CloudStreams.Core.Data;
using System.Text;

namespace CloudStreams.Core;

/// <summary>
/// Defines extensions for <see cref="CloudEvent"/>
/// </summary>
public static class CloudEventExtensions
{

    /// <summary>
    /// Attempts to get the <see cref="CloudEvent"/> attribute with the specified name
    /// </summary>
    /// <param name="e">The <see cref="CloudEvent"/> to get the attribute of</param>
    /// <param name="attributeName">The name of the context attribute to get</param>
    /// <param name="value">The value of the <see cref="CloudEvent"/>'s attribute, if any</param>
    /// <returns>A boolean indicating whether or not the <see cref="CloudEvent"/> containing the specified attribute</returns>
    public static bool TryGetAttribute(this CloudEvent e, string attributeName, out object? value)
    {
        value = e.GetAttribute(attributeName);
        return value != null;
    }

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
            string str => ulong.Parse(str),
            ulong num => num,
            JsonElement jsonElem => Hylo.Serializer.Json.Deserialize<ulong?>(jsonElem),
            _ => null
        };
    }

    /// <summary>
    /// Converts the <see cref="CloudEvent"/> into a new <see cref="HttpContent"/>
    /// </summary>
    /// <param name="e">The <see cref="CloudEvent"/> to convert to a new <see cref="HttpContent"/></param>
    /// <returns>The <see cref="CloudEvent"/>'s <see cref="HttpContent"/> representation</returns>
    public static HttpContent ToHttpContent(this CloudEvent e)
    {
        return new StringContent(Hylo.Serializer.Json.Serialize(e), Encoding.UTF8, CloudEventMediaTypeNames.CloudEventsJson);
    }

    /// <summary>
    /// Gets the <see cref="CloudEvent"/>'s context attributes
    /// </summary>
    /// <param name="e">The <see cref="CloudEvent"/> to get the context attributes of</param>
    /// <param name="includeExtensionAttributes">A boolean indicating whether or not to include extension attributes</param>
    /// <returns>A new <see cref="IEnumerable{T}"/></returns>
    public static IEnumerable<KeyValuePair<string, object>> GetContextAttributes(this CloudEvent e, bool includeExtensionAttributes = true)
    {
        yield return new(CloudEventAttributes.Id, e.Id);
        yield return new(CloudEventAttributes.SpecVersion, e.SpecVersion);
        if (e.Time.HasValue) yield return new(CloudEventAttributes.Time, e.Time);
        yield return new(CloudEventAttributes.Source, e.Source);
        yield return new(CloudEventAttributes.Type, e.Type);
        if (!string.IsNullOrWhiteSpace(e.Subject)) yield return new(CloudEventAttributes.Subject, e.Subject);
        if (!string.IsNullOrWhiteSpace(e.DataContentType)) yield return new(CloudEventAttributes.DataContentType, e.DataContentType);
        if (e.DataSchema != null) yield return new(CloudEventAttributes.DataSchema, e.DataSchema);
        if (!includeExtensionAttributes || e.ExtensionAttributes == null) yield break;
        foreach (var extensionAttribute in e.ExtensionAttributes)
        {
            yield return extensionAttribute;
        }
    }

}
