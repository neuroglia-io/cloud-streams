namespace CloudStreams.Core;

/// <summary>
/// Exposes methods to help handling <see cref="Uri"/>s
/// </summary>
public static class UriHelper
{

    /// <summary>
    /// Combines the specified components<para></para>
    /// Leading and trailing slash characters will be removed from all components
    /// </summary>
    /// <param name="components">The components to combine</param>
    /// <returns>A new <see cref="Uri"/></returns>
    public static Uri Combine(params string[] components) => new(string.Join('/', components.Select(c => c.Trim('/'))));

}
