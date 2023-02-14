namespace CloudStreams;

/// <summary>
/// Enumerates all supported cloud event stream partition types
/// </summary>
public static class CloudEventPartitionType
{

    /// <summary>
    /// Indicates a partition by cloud event source
    /// </summary>
    public const string BySource = "by-source";
    /// <summary>
    /// Indicates a partition by cloud event type
    /// </summary>
    public const string ByType = "by-type";
    /// <summary>
    /// Indicates a partition by subject
    /// </summary>
    public const string BySubject = "by-subject";

}