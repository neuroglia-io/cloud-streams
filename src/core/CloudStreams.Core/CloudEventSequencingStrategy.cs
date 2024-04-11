namespace CloudStreams.Core;

/// <summary>
/// Enumerates default sequencing strategies for cloud events
/// </summary>
public static class CloudEventSequencingStrategy
{

    /// <summary>
    /// Indicates that cloud events should not be sequenced by CloudStreams
    /// </summary>
    public const string None = "none";
    /// <summary>
    /// Indicates that cloud events should be sequenced by CloudStreams using the specified context attribute
    /// </summary>
    public const string Attribute = "attribute";

}