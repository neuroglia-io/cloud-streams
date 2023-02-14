namespace CloudStreams;

/// <summary>
/// Enumerates all default types of resource-related event
/// </summary>
public static class ResourceWatchEventType
{

    /// <summary>
    /// Indicates an event that describes the creation of a resource
    /// </summary>
    public const string Created = "created";
    /// <summary>
    /// Indicates an event that describes the update of a resource
    /// </summary>
    public const string Updated = "updated";
    /// <summary>
    /// Indicates an event that describes the deletion of a resource
    /// </summary>
    public const string Deleted = "deleted";
    /// <summary>
    /// Indicates an event that describes a resource-related error
    /// </summary>
    public const string Error = "error";

}
