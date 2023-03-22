namespace CloudStreams.Core;

/// <summary>
/// Defines the fundamentals of an event produced by a watch
/// </summary>
public interface IResourceWatchEvent
{

    /// <summary>
    /// Gets the event's type<para></para>
    /// </summary>
    ResourceWatchEventType Type { get; }

    /// <summary>
    /// Gets the object that has produced the <see cref="IResourceWatchEvent"/>
    /// </summary>
    IResource Resource { get; }

}

/// <summary>
/// Defines the fundamentals of an event produced by a watch
/// </summary>
public interface IResourceWatchEvent<TResource>
    : IResourceWatchEvent
    where TResource : class, IResource, new()
{

    /// <summary>
    /// Gets the object that has produced the <see cref="IResourceWatchEvent"/>
    /// </summary>
    new TResource Resource { get; }

}
