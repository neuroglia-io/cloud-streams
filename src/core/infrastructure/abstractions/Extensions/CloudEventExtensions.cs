namespace CloudStreams.Core.Infrastructure;

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

}
