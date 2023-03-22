using CloudStreams.Core.Serialization.Json.Converters;

namespace CloudStreams.Core;

/// <summary>
/// Enumerates all types of cloud event mutations
/// </summary>
[JsonConverter(typeof(StringEnumConverterFactory))]
public enum CloudEventMutationType
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
