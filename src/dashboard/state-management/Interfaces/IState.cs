namespace CloudStreams.Dashboard.StateManagement;

/// <summary>
/// Defines the fundamentals of a Flux state
/// </summary>
public interface IState
{

    /// <summary>
    /// Gets the <see cref="IState"/>'s value
    /// </summary>
    object Value { get; }

    /// <summary>
    /// Attempts to dispatch the specified Flux action
    /// </summary>
    /// <param name="action">The Flux action to dispatch</param>
    /// <returns>A boolean indicating whether or not the Flux action could be dispatched</returns>
    bool TryDispatch(object action);

    /// <summary>
    /// Adds an <see cref="IReducer"/> to the state
    /// </summary>
    /// <param name="reducer">The <see cref="IReducer"/> to add</param>
    void AddReducer(IReducer reducer);

}

/// <summary>
/// Defines the fundamentals of a Flux state
/// </summary>
/// <typeparam name="TState">The type of the <see cref="IState"/>'s value</typeparam>
public interface IState<TState>
    : IState, IObservable<TState>
{

    /// <summary>
    /// Gets the <see cref="IState"/>'s value
    /// </summary>
    new TState Value { get; }

    /// <summary>
    /// Adds an <see cref="IReducer"/> to the state
    /// </summary>
    /// <param name="reducer">The <see cref="IReducer"/> to add</param>
    void AddReducer(IReducer<TState> reducer);

}
