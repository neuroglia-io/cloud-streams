using Neuroglia.Serialization;
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
            string str => ulong.Parse(str),
            ulong num => num,
            JsonElement jsonElem => Neuroglia.Serialization.Json.JsonSerializer.Default.Deserialize<ulong?>(jsonElem),
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

}
