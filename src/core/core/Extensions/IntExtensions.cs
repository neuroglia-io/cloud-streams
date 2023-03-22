namespace CloudStreams.Core;

/// <summary>
/// Defines extensions for <see cref="int"/>s
/// </summary>
public static class IntExtensions
{

    /// <summary>
    /// Determines whether or not the value represents a successfull status code
    /// </summary>
    /// <param name="statusCode">The value to check</param>
    /// <returns>A boolean indicating whether or not the value represents a successfull status code</returns>
    public static bool IsSuccessStatusCode(this int statusCode) => statusCode >= 200 && statusCode < 300;

}