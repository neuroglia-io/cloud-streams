using CloudStreams.Core.Resources;
using Neuroglia.Eventing.CloudEvents;
using Neuroglia.Serialization.Json;
using System.Text.Json.Nodes;

namespace CloudStreams.Core.Infrastructure;

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
    public static CloudEvent ToCloudEvent(this CloudEventDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        var e = (JsonObject)JsonSerializer.Default.SerializeToNode(descriptor.Metadata.ContextAttributes)!;
        var data = JsonSerializer.Default.SerializeToNode(descriptor.Data);
        e[CloudEventAttributes.Data] = data;
        return JsonSerializer.Default.Deserialize<CloudEvent>(e)!;
    }

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