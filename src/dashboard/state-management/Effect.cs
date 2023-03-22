namespace CloudStreams.Dashboard.StateManagement;

/// <summary>
/// Represents the default implementation of the <see cref="IEffect{TAction}"/> interface
/// </summary>
/// <typeparam name="TAction">The type of action to apply the effect to</typeparam>
public class Effect<TAction>
    : IEffect<TAction>
{

    /// <summary>
    /// Initializes a new <see cref="IEffect{TAction}"/>
    /// </summary>
    /// <param name="effectFunction">The effect <see cref="Func{T, TResult}"/></param>
    public Effect(Func<TAction, IEffectContext, Task> effectFunction)
    {
        if(effectFunction == null)
            throw new ArgumentNullException(nameof(effectFunction));
        this.EffectFunction = effectFunction;
    }

    /// <summary>
    /// Gets the effect <see cref="Func{T, TResult}"/>
    /// </summary>
    protected Func<TAction, IEffectContext, Task> EffectFunction { get; }

    /// <inheritdoc/>
    public virtual bool AppliesTo(object action)
    {
        if(action == null)  
           throw new ArgumentNullException(nameof(action));
        return action is TAction;
    }

    /// <inheritdoc/>
    public virtual async Task ApplyAsync(TAction action, IEffectContext context)
    {
        await this.EffectFunction(action, context);
    }

    async Task IEffect.ApplyAsync(object action, IEffectContext context)
    {
        await this.ApplyAsync((TAction)action, context).ConfigureAwait(false);
    }

}
