using Neuroglia.Serialization.Json.Converters;

namespace CloudStreams.Core;

/// <summary>
/// Enumerates all types of cloud event mutations
/// </summary>
[TypeConverter(typeof(EnumMemberTypeConverter))]
[JsonConverter(typeof(StringEnumConverter))]
public enum CloudEventMutationType
{
    /// <summary>
    /// Indicates an expression-based mutation
    /// </summary>
    [EnumMember(Value = "expression")]
    Expression = 1,
    /// <summary>
    /// Indicates a webhook-based mutation
    /// </summary>
    [EnumMember(Value = "webhook")]
    Webhook = 2
}