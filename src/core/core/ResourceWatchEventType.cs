namespace CloudStreams.Core;

/// <summary>
/// Enumerates all default types of resource-related event
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ResourceWatchEventType
{
    /// <summary>
    /// Indicates an event that describes the creation of a resource
    /// </summary>
    [EnumMember(Value = "created")]
    Created = 0,
    /// <summary>
    /// Indicates an event that describes the update of a resource
    /// </summary>
    [EnumMember(Value = "updated")]
    Updated = 1,
    /// <summary>
    /// Indicates an event that describes the deletion of a resource
    /// </summary>
    [EnumMember(Value = "deleted")]
    Deleted = 2,
    /// <summary>
    /// Indicates an event that describes a resource-related error
    /// </summary>
    [EnumMember(Value = "error")]
    Error = 4
}
