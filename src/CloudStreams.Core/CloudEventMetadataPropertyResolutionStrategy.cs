using Neuroglia.Serialization.Json.Converters;

namespace CloudStreams.Core;

/// <summary>
/// Enumerates all strategies for resolving cloud event metadata properties
/// </summary>
[TypeConverter(typeof(EnumMemberTypeConverter))]
[JsonConverter(typeof(StringEnumConverter))]
public enum CloudEventMetadataPropertyResolutionStrategy
{
    /// <summary>
    /// Indicates that the metadata property is extracted from a context attribute
    /// </summary>
    [EnumMember(Value = "attribute")]
    Attribute = 1,
    /// <summary>
    /// Indicates that the metadata property is extracted by evaluating a runtime expression against the event
    /// </summary>
    [EnumMember(Value = "expression")]
    Expression = 2
}
