using System.Text.RegularExpressions;
using YamlDotNet.Serialization.NamingConventions;

namespace CloudStreams.Core;

/// <summary>
/// Defines extensions for <see cref="string"/>s
/// </summary>
public static class StringExtensions
{

    private static readonly Regex MatchCurlyBracedWords = new(@"\{([^}]+)\}", RegexOptions.Compiled);

    /// <summary>
    /// Converts the specified input into a camel-cased string
    /// </summary>
    /// <param name="input">The string to convert to camel-case</param>
    /// <returns>The camel-cased input</returns>
    public static string ToCamelCase(this string input) => CamelCaseNamingConvention.Instance.Apply(input);

    /// <summary>
    /// Converts the specified input into a hyphen-cased string
    /// </summary>
    /// <param name="input">The string to convert to hyphen-case</param>
    /// <returns>The hyphen-cased input</returns>
    public static string ToHyphenCase(this string input) => HyphenatedNamingConvention.Instance.Apply(input);

    /// <summary>
    /// Formats the string
    /// </summary>
    /// <param name="text">The string to format</param>
    /// <param name="args">The arguments to format the string with</param>
    /// <remarks>Accepts named arguments, which will be replaced in sequence by the specified values</remarks>
    /// <returns>The resulting string</returns>
    public static string Format(this string text, params object[] args)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;
        string formattedText = text;
        List<string> matches = MatchCurlyBracedWords.Matches(text)
            .Select(m => m.Value)
            .Distinct()
            .ToList();
        for (int i = 0; i < matches.Count && i < args.Length; i++)
        {
            formattedText = formattedText.Replace(matches[i], args[i].ToString());
        }
        return formattedText;
    }

}
