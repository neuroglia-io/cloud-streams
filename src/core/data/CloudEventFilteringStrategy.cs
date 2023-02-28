using CloudStreams.Core.Serialization.Json.Converters;

namespace CloudStreams.Core;

/// <summary>
/// Enumerates all supported filtering strategies for consumed cloud events
/// </summary>
[JsonConverter(typeof(StringEnumConverterFactory))]
public enum CloudEventFilteringStrategy
{
    /// <summary>
    /// Indicates a context attributes based filtering
    /// </summary>
    [EnumMember(Value = "attributes")]
    Attributes,
    /// <summary>
    /// Indicates an expression-based filtering
    /// </summary>
    [EnumMember(Value = "expression")]
    Expression
}
