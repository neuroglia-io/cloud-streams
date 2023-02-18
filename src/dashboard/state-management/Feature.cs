using System.Reactive.Subjects;

namespace CloudStreams.Dashboard.StateManagement;

/// <summary>
/// Represents the default implementation of the <see cref="IFeature"/> interface
/// </summary>
/// <typeparam name="TState">The type of the <see cref="IFeature"/>'s state</typeparam>
public class Feature<TState>
    : IFeature<TState>
{

    /// <summary>
    /// Initializes a new <see cref="Feature{TState}"/>
    /// </summary>
    /// <param name="value">The <see cref="Feature{TState}"/>'s value</param>
    public Feature(TState value)
    {
        if(value == null)
            throw new ArgumentNullException(nameof(value)); 
        this.Stream = new();
        this.State = value;
    }

    private TState _State = default!;
    /// <inheritdoc/>
    public TState State
    {
        get => this._State;
        set
        {
            this._State = value;
            this.Stream.OnNext(value);
        }
    }

    object IFeature.State => this.State!;

    /// <summary>
    /// Gets the <see cref="BehaviorSubject{T}"/> used to stream the <see cref="Feature{TState}"/> changes
    /// </summary>
    protected Subject<TState> Stream { get; }

    /// <summary>
    /// Gets a <see cref="Dictionary{TKey, TValue}"/> containing the type/<see cref="IReducer"/>s mappings
    /// </summary>
    protected Dictionary<Type, List<IReducer<TState>>> Reducers { get; } = new();

    /// <inheritdoc/>
    public virtual void AddReducer(IReducer<TState> reducer)
    {
        if (reducer == null)
            throw new ArgumentNullException(nameof(reducer));
        var genericReducerType = reducer.GetType().GetGenericType(typeof(IReducer<,>));
        var actionType = genericReducerType.GetGenericArguments()[1];
        if (this.Reducers.TryGetValue(actionType, out var reducers))
            reducers.Add(reducer);
        else
            this.Reducers.Add(actionType, new() { reducer });
    }

    void IFeature.AddReducer(IReducer reducer)
    {
        if (reducer == null)
            throw new ArgumentNullException(nameof(reducer));
        this.AddReducer((IReducer<TState>)reducer);
    }

    /// <inheritdoc/>
    public virtual IDisposable Subscribe(IObserver<TState> observer)
    {
        return this.Stream.Subscribe(observer);
    }

    /// <inheritdoc/>
    public virtual bool ShouldReduceStateFor(object action)
    {
        if (action == null)
            throw new ArgumentNullException(nameof(action));
        return this.Reducers.ContainsKey(action.GetType());
    }
    
    /// <inheritdoc/>
    public virtual async Task ReduceStateAsync(IActionContext context, Func<DispatchDelegate, DispatchDelegate> reducerPipelineBuilder)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));
        if (reducerPipelineBuilder == null)
            throw new ArgumentNullException(nameof(reducerPipelineBuilder));
            var pipeline = reducerPipelineBuilder(ApplyReducersAsync);
        this.State = (TState)await pipeline(context);
    }

    /// <summary>
    /// Applies all the <see cref="IReducer"/> matching the specified <see cref="IActionContext"/>
    /// </summary>
    /// <param name="context">The <see cref="IActionContext"/> to apply the <see cref="IReducer"/>s to</param>
    /// <returns>The reduced <see cref="IFeature"/>'s state</returns>
    protected virtual async Task<object> ApplyReducersAsync(IActionContext context)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));
        return (await Task.Run(() =>
        {
            var newState = this.State;
            if (this.Reducers.TryGetValue(context.Action.GetType(), out var reducers))
            {
                foreach (var reducer in reducers)
                {
                    newState = reducer.Reduce(newState, context.Action);
                }
            }
            return newState;
        }))!;
    }

}
