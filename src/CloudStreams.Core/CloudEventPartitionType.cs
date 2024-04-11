using Neuroglia.Serialization.Json.Converters;

namespace CloudStreams.Core;

/// <summary>
/// Enumerates all supported cloud event stream partition types
/// </summary>
[TypeConverter(typeof(EnumMemberTypeConverter))]
[JsonConverter(typeof(StringEnumConverter))]
public enum CloudEventPartitionType
{
    /// <summary>
    /// Indicates a partition by cloud event source
    /// </summary>
    [EnumMember(Value = "by-source")]
    BySource = 1,
    /// <summary>
    /// Indicates a partition by cloud event type
    /// </summary>
    [EnumMember(Value = "by-type")]
    ByType = 2,
    /// <summary>
    /// Indicates a partition by subject
    /// </summary>
    [EnumMember(Value = "by-subject")]
    BySubject = 4,
    /// <summary>
    /// Indicates a partition by $correlationId
    /// </summary>
    [EnumMember(Value = "by-correlation-id")]
    ByCorrelationId = 5,
    /// <summary>
    /// Indicates a partition by $causationId
    /// </summary>
    [EnumMember(Value = "by-causation-id")]
    ByCausationId = 6
}
