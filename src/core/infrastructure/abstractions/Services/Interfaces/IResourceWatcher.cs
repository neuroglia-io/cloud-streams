namespace CloudStreams.Core.Infrastructure.Services;

/// <summary>
/// Defines the fundamentals of a service used to watch resources
/// </summary>
public interface IResourceWatcher
    : IObservable<IResourceWatchEvent>, IDisposable
{



}

/// <summary>
/// Defines the fundamentals of a service used to watch resources
/// </summary>
/// <typeparam name="TResource">The type of resource to watch</typeparam>
public interface IResourceWatcher<TResource>
    : IObservable<IResourceWatchEvent<TResource>>, IDisposable
    where TResource : class, IResource, new()
{



}
