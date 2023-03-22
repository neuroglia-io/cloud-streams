namespace CloudStreams.Dashboard.StateManagement;

/// <summary>
/// Defines the fundamentals of an effect
/// </summary>
public interface IEffect
{

    /// <summary>
    /// Determines whether or not to apply the effect
    /// </summary>
    /// <param name="action">THe action to apply the effect to</param>
    /// <returns>A boolean indicating whether or not to apply the effect to the specified action</returns>
    bool AppliesTo(object action);

    /// <summary>
    /// Applies the effect to the specified action
    /// </summary>
    /// <param name="action">The action to apply the effect to</param>
    /// <param name="context">The <see cref="IEffect"/> context</param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    Task ApplyAsync(object action, IEffectContext context);

}

/// <summary>
/// Defines the fundamentals of an effect
/// </summary>
/// <typeparam name="TAction">The type of the action to apply the effect to</typeparam>
public interface IEffect<TAction>
    : IEffect
{

    /// <summary>
    /// Applies the effect to the specified action
    /// </summary>
    /// <param name="action">The action to apply the effect to</param>
    /// <param name="context">The <see cref="IEffect"/> context</param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    Task ApplyAsync(TAction action, IEffectContext context);

}
