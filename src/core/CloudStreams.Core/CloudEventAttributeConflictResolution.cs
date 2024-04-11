namespace CloudStreams.Core;

/// <summary>
/// Enumerates default resolution strategies to adopt when handling conflict with existing cloud event context attributes
/// </summary>
public static class CloudEventAttributeConflictResolution
{

    /// <summary>
    /// Indicates that the value of the existing attribute should be overwritten
    /// </summary>
    public const string Overwrite = "overwrite";
    /// <summary>
    /// Indicates that the value be written to a fallback attribute when the target attribute exists
    /// </summary>
    public const string Fallback = "fallback";

}
