namespace CloudStreams.Core;

/// <summary>
/// Defines the fundamentals of a resource
/// </summary>
public interface IResource
    : IExtensible, IMetadata<ResourceMetadata>
{

    /// <summary>
    /// Gets the resource's API version
    /// </summary>
    string ApiVersion { get; }

    /// <summary>
    /// Gets the resource's kind
    /// </summary>
    string Kind { get; }

    /// <summary>
    /// Gets an object used to describe the resource's type
    /// </summary>
    ResourceType Type { get; }

}

/// <summary>
/// Defines the fundamentals of a resource
/// </summary>
/// <typeparam name="TSpec">The type of the <see cref="IResource"/>'s spec</typeparam>
public interface IResource<TSpec>
    : IResource, ISpec<TSpec>
    where TSpec : class, new()
{



}

/// <summary>
/// Defines the fundamentals of a resource
/// </summary>
/// <typeparam name="TSpec">The type of the <see cref="IResource"/>'s spec</typeparam>
/// <typeparam name="TStatus">The type of the <see cref="IResource"/>'s status</typeparam>
public interface IResource<TSpec, TStatus>
    : IResource<TSpec>, IStatus<TStatus>
    where TSpec : class, new()
    where TStatus : class, new()
{



}