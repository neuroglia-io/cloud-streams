using Neuroglia.Serialization.Json.Converters;

namespace CloudStreams.Core;

/// <summary>
/// Enumerates all supported retry backoff duration types
/// </summary>
[TypeConverter(typeof(EnumMemberTypeConverter))]
[JsonConverter(typeof(StringEnumConverter))]
public enum RetryBackoffDurationType
{
    /// <summary>
    /// Indicates a constant duration
    /// </summary>
    [EnumMember(Value = "constant")]
    Constant = 1,
    /// <summary>
    /// Indicates a duration that increments at every retry attempt in a constant fashion
    /// </summary>
    [EnumMember(Value = "incremental")]
    Incremental = 2,
    /// <summary>
    /// Indicates an exponential duration
    /// </summary>
    [EnumMember(Value = "exponential")]
    Exponential = 4
}
