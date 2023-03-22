namespace CloudStreams.Dashboard.StateManagement;

/// <summary>
/// Defines the fundamentals of a Flux reducer
/// </summary>
public interface IReducer
{

    /// <summary>
    /// Reduces the specified state, for the specified action
    /// </summary>
    /// <param name="state">The state to reduce</param>
    /// <param name="action">The Flux action the reducer applies to</param>
    /// <returns>A new object that represents the reduced state</returns>
    object Reduce(object state, object action);

}

/// <summary>
/// Defines the fundamentals of a Flux reducer
/// </summary>
/// <typeparam name="TState">The type of state to reduce</typeparam>
public interface IReducer<TState>
    : IReducer
{

    /// <summary>
    /// Reduces the specified state, for the specified action
    /// </summary>
    /// <param name="state">The state to reduce</param>
    /// <param name="action">The Flux action the reducer applies to</param>
    /// <returns>A new object that represents the reduced state</returns>
    TState Reduce(TState state, object action);

}

/// <summary>
/// Defines the fundamentals of a Flux reducer
/// </summary>
/// <typeparam name="TState">The type of state to reduce</typeparam>
/// <typeparam name="TAction">The type of flux action the reducer applies to</typeparam>
public interface IReducer<TState, TAction>
    : IReducer<TState>
{

    /// <summary>
    /// Reduces the specified state, for the specified action
    /// </summary>
    /// <param name="state">The state to reduce</param>
    /// <param name="action">The Flux action the reducer applies to</param>
    /// <returns>A new object that represents the reduced state</returns>
    TState Reduce(TState state, TAction action);

}
