using CloudStreams.Core.Data.Models;

namespace CloudStreams.Core;

/// <summary>
/// Defines extensions for <see cref="Response"/>s
/// </summary>
public static class ResponseExtensions
{

    /// <summary>
    /// Determines whether or not the <see cref="Response"/> defines a success status
    /// </summary>
    /// <param name="response">The <see cref="Response"/> to check</param>
    /// <returns>A boolean indicating whether or not the <see cref="Response"/> defines a success status</returns>
    public static bool IsSuccessStatusCode(this Response response) => response.Status is >= 200 and < 300;

}
