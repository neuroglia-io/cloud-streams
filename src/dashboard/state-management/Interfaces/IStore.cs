namespace CloudStreams.Dashboard.StateManagement;

/// <summary>
/// Defines the fundamentals of a Flux store
/// </summary>
public interface IStore
    : IObservable<object>
{

    /// <summary>
    /// Gets the <see cref="IStore"/>'s state
    /// </summary>
    object State { get; }

    /// <summary>
    /// Adds a new <see cref="IFeature"/> to the store
    /// </summary>
    /// <typeparam name="TState">The type of state managed by the <see cref="IFeature"/> to add</typeparam>
    /// <param name="feature">The <see cref="IFeature"/> to add</param>
    void AddFeature<TState>(IFeature<TState> feature);

    /// <summary>
    /// Adds a new <see cref="IMiddleware"/> to the store
    /// </summary>
    void AddMiddleware(Type middlewareType);

    /// <summary>
    /// Adds a new <see cref="IEffect"/> to the store
    /// </summary>
    /// <param name="effect">The <see cref="IEffect"/> to add</param>
    void AddEffect(IEffect effect);

    /// <summary>
    /// Gets the <see cref="IFeature"/> of the specified type
    /// </summary>
    /// <typeparam name="TState">The type of state managed by the <see cref="IFeature"/> to get</typeparam>
    /// <returns>The <see cref="IFeature"/> with the specified state type</returns>
    IFeature<TState> GetFeature<TState>();

}
