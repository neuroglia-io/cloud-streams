using Neuroglia.Serialization.Json.Converters;

namespace CloudStreams.Core;

/// <summary>
/// Enumerates resource label selection operators 
/// </summary>
[TypeConverter(typeof(EnumMemberTypeConverter))]
[JsonConverter(typeof(StringEnumConverter))]
public enum SubscriptionStatusPhase
{
    /// <summary>
    /// Indicates that the subscription is inactive because its broker is inactive, or because the later did not pick it up
    /// </summary>
    [EnumMember(Value = "inactive")]
    Inactive = 1,
    /// <summary>
    /// Indicates that the subscription is being monitored by its broker
    /// </summary>
    [EnumMember(Value = "active")]
    Active = 2
}