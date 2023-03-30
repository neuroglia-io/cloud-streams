using System.Xml;

namespace CloudStreams;

/// <summary>
/// Represents an helper class for handling ISO 8601 timespans
/// </summary>
public static class Iso8601TimeSpan
{

    /// <summary>
    /// Parses the specified input
    /// </summary>
    /// <param name="input">The input string to parse</param>
    /// <returns>The parsed <see cref="TimeSpan"/></returns>
    public static TimeSpan Parse(string input) => Duration.Parse(input).ToTimeSpan();

    /// <summary>
    /// Formats the specified <see cref="TimeSpan"/>
    /// </summary>
    /// <param name="timeSpan">The <see cref="TimeSpan"/> to format</param>
    /// <returns>The parsed <see cref="TimeSpan"/></returns>
    public static string Format(TimeSpan timeSpan) => XmlConvert.ToString(timeSpan);

}