namespace CloudStreams;

/// <summary>
/// Enumerates all supported cloud event stream partition types
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CloudEventPartitionType
{
    /// <summary>
    /// Indicates a partition by cloud event source
    /// </summary>
    [EnumMember(Value = "by-source")]
    BySource = 0,
    /// <summary>
    /// Indicates a partition by cloud event type
    /// </summary>
    [EnumMember(Value = "by-type")]
    ByType = 1,
    /// <summary>
    /// Indicates a partition by subject
    /// </summary>
    [EnumMember(Value = "by-subject")]
    BySubject = 2
}