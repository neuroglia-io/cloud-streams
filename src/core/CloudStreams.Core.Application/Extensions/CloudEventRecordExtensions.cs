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

using Neuroglia.Serialization.Json;
using System.Text.Json.Nodes;

namespace CloudStreams.Core.Application;

/// <summary>
/// Defines extensions for <see cref="CloudEventRecord"/>s
/// </summary>
public static class CloudEventRecordExtensions
{

    /// <summary>
    /// Converts the <see cref="CloudEventDescriptor"/> into the <see cref="CloudEvent"/> it describes
    /// </summary>
    /// <param name="descriptor">The <see cref="CloudEventDescriptor"/> to convert</param>
    /// <returns>The <see cref="CloudEvent"/> described by the converted <see cref="CloudEventDescriptor"/></returns>
    /// <remarks>
    /// Binary data is stored with the context attributes, as <c>data_base64</c>, which is not a valid attribute name and is therefore restored explicitly (see #208)
    /// </remarks>
    public static CloudEvent ToCloudEvent(this CloudEventDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        var e = (JsonObject)JsonSerializer.Default.SerializeToNode(descriptor.Metadata.ContextAttributes.Where(a => a.Key.IsLowercased() && a.Key.IsAlphanumeric()).ToDictionary(kvp => kvp.Key, kvp => kvp.Value))!;
        if (IsNull(descriptor.Data) && descriptor.Metadata.ContextAttributes.TryGetValue(CloudEventAttributes.DataBase64, out var dataBase64) && dataBase64?.ToString() is { Length: > 0 } base64) e[CloudEventAttributes.DataBase64] = base64;
        else e[CloudEventAttributes.Data] = JsonSerializer.Default.SerializeToNode(descriptor.Data);
        return JsonSerializer.Default.Deserialize<CloudEvent>(e)!;
    }

    /// <summary>
    /// Determines whether the specified data is null, which the data of a stored event without data is, as read back from the event store: JSON <c>null</c>
    /// </summary>
    static bool IsNull(object? data) => data switch
    {
        null => true,
        System.Text.Json.JsonElement element => element.ValueKind is System.Text.Json.JsonValueKind.Null or System.Text.Json.JsonValueKind.Undefined,
        _ => false
    };

    /// <summary>
    /// Converts the <see cref="CloudEventRecord"/> into the <see cref="CloudEvent"/> it describes
    /// </summary>
    /// <param name="record">The <see cref="CloudEventRecord"/> to convert</param>
    /// <param name="sequencingConfiguration">An object used to configure the sequencing strategy to use</param>
    /// <returns>The <see cref="CloudEvent"/> described by the converted <see cref="CloudEventRecord"/></returns>
    public static CloudEvent ToCloudEvent(this CloudEventRecord record, CloudEventSequencingConfiguration? sequencingConfiguration)
    {
        ArgumentNullException.ThrowIfNull(record);
        sequencingConfiguration ??= CloudEventSequencingConfiguration.Default;
        var e = record.ToCloudEvent();
        if (sequencingConfiguration.Strategy == CloudEventSequencingStrategy.None) return e;
        e.ExtensionAttributes ??= new Dictionary<string, object>();
        if (e.ExtensionAttributes.ContainsKey(sequencingConfiguration.AttributeName!) && sequencingConfiguration.AttributeConflictResolution == CloudEventAttributeConflictResolution.Fallback) e.ExtensionAttributes[sequencingConfiguration.FallbackAttributeName!] = record.Sequence;
        else e.ExtensionAttributes[sequencingConfiguration.AttributeName!] = record.Sequence;
        return e;
    }

}