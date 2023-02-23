namespace CloudStreams.Core;

/// <summary>
/// Defines the fundamentals of a reference to a resource
/// </summary>
public interface IResourceReference
{

    /// <summary>
    /// Gets/sets the name of the referenced resource
    /// </summary>
    string ApiVersion { get; }

    /// <summary>
    /// Gets/sets the name of the referenced resource
    /// </summary>
    string Kind { get; }

    /// <summary>
    /// Gets/sets the name of the referenced resource
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets/sets the name of the referenced resource
    /// </summary>
    string? Namespace { get; }

}
