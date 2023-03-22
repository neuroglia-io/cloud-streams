using CloudStreams.Core.Serialization.Json.Converters;

namespace CloudStreams.Core;

/// <summary>
/// Enumerates all cloud event filter types
/// </summary>
[JsonConverter(typeof(StringEnumConverterFactory))]
public enum CloudEventFilterType
{
    /// <summary>
    /// Indicates a context attributes based filter
    /// </summary>
    [EnumMember(Value = "attributes")]
    Attributes,
    /// <summary>
    /// Indicates an expression-based filter
    /// </summary>
    [EnumMember(Value = "expression")]
    Expression
}
