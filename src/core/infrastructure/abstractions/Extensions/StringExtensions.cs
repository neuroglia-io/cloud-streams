namespace CloudStreams.Core.Infrastructure;

/// <summary>
/// Defines extensions for <see cref="string"/>s
/// </summary>
public static class StringExtensions
{

    /// <summary>
    /// Determines whether or not the string is a runtime expression
    /// </summary>
    /// <param name="input">The string to check</param>
    /// <returns>A boolean indicating whether or not the string is a runtime expression</returns>
    public static bool IsRuntimeExpression(this string input) => input.TrimStart().StartsWith("${") && input.TrimEnd().EndsWith("}");

}