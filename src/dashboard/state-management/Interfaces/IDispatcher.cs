namespace CloudStreams.Dashboard.StateManagement;

/// <summary>
/// Defines the fundamentals of a service used to dispatch Flux actions
/// </summary>
public interface IDispatcher
    : IObservable<object>
{

    /// <summary>
    /// Dispatches the specified action
    /// </summary>
    /// <param name="action">The action to dispatch</param>
    void Dispatch(object action);

}