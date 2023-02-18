namespace CloudStreams.Core.Serialization.Json;

/// <summary>
/// Enumerates all supported types of <see cref="JsonObjectPropertyReferenceType"/>s
/// </summary>
public enum JsonObjectPropertyReferenceType
{
    /// <summary>
    /// Indicates a simple property name
    /// </summary>
    Name,
    /// <summary>
    /// Indicates a complex property path (ex: 'foo.bar.baz')
    /// </summary>
    Path
}