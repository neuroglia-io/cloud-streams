using CloudStreams.Core.Data.Models;
using System.Text;
using System.Text.Json;

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
            JsonElement jsonElem => Serializer.Json.Deserialize<ulong?>(jsonElem), 
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
        return new StringContent(Serializer.Json.Serialize(e), Encoding.UTF8, CloudEventMediaTypeNames.CloudEventsJson);
    }

}
