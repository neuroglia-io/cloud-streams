using CloudStreams.Core.Serialization.Json.Converters;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace CloudStreams.Core;

/// <summary>
/// Enumerates all supported cloud event stream partition types
/// </summary>
[TypeConverter(typeof(EnumMemberConverter))]
[JsonConverter(typeof(StringEnumConverterFactory))]
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
    BySubject = 4
}