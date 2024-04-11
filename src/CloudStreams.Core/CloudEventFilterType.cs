using Neuroglia.Serialization.Json.Converters;

namespace CloudStreams.Core;

/// <summary>
/// Enumerates all cloud event filter types
/// </summary>
[TypeConverter(typeof(EnumMemberTypeConverter))]
[JsonConverter(typeof(StringEnumConverter))]
public enum CloudEventFilterType
{
    /// <summary>
    /// Indicates a context attributes based filter
    /// </summary>
    [EnumMember(Value = "attributes")]
    Attributes = 1,
    /// <summary>
    /// Indicates an expression-based filter
    /// </summary>
    [EnumMember(Value = "expression")]
    Expression = 2
}