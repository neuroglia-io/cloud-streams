using CloudStreams.Core.Serialization.Json.Converters;

namespace CloudStreams.Core;

/// <summary>
/// Enumerates all supported mutation strategies for consumed cloud events
/// </summary>
[JsonConverter(typeof(StringEnumConverterFactory))]
public enum CloudEventMutationStrategy
{
    /// <summary>
    /// Indicates an expression-based mutation
    /// </summary>
    [EnumMember(Value = "expression")]
    Expression,
    /// <summary>
    /// Indicates a webhook-based expression
    /// </summary>
    [EnumMember(Value = "webhook")]
    Webhook
}
