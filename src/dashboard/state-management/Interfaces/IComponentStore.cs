namespace CloudStreams.Dashboard.StateManagement;

/// <summary>
/// Defines the fundamentals of a service used to store a component's state
/// </summary>
public interface IComponentStore<TState>
    : IObservable<TState>, IDisposable, IAsyncDisposable
{

    /// <summary>
    /// Initializes the <see cref="IComponentStore{TState}"/>
    /// </summary>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    Task InitializeAsync();

}
