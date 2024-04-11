namespace CloudStreams.Core;

/// <summary>
/// Enumerates default cloud event validation strategies
/// </summary>
public static class CloudEventValidationStrategy
{

    /// <summary>
    /// Indicates that no validation is performed
    /// </summary>
    public const string None = "none";
    /// <summary>
    /// Indicates that validation is performed and errors are treated as warnings
    /// </summary>
    public const string Warn = "warn";
    /// <summary>
    /// Indicates that validation fails on errors
    /// </summary>
    public const string Fail = "fail";

}