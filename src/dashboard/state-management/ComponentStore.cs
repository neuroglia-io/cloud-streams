using System.Reactive.Subjects;

namespace CloudStreams.Dashboard.StateManagement;

/// <summary>
/// Represents the base class for all component stores
/// </summary>
/// <typeparam name="TState">The type of the component store's state</typeparam>
public abstract class ComponentStore<TState>
    : IComponentStore<TState>
{
    Subject<TState> _Subject;
    TState _State;
    bool _Disposed;

    /// <summary>
    /// Initializes a new <see cref="ComponentStore{TState}"/>
    /// </summary>
    /// <param name="state">The store's initial state</param>
    protected ComponentStore(TState state)
    {
        this.CancellationTokenSource = new CancellationTokenSource();
        this._Subject = new();
        this._State = state;
    }

    /// <summary>
    /// Gets the <see cref="ComponentStore{TState}"/>'s <see cref="System.Threading.CancellationTokenSource"/>
    /// </summary>
    protected CancellationTokenSource CancellationTokenSource { get; }

    /// <inheritdoc/>
    public virtual Task InitializeAsync() => Task.CompletedTask;

    /// <summary>
    /// Sets the <see cref="ComponentStore{TState}"/>'s state
    /// </summary>
    /// <param name="state">The updated state to set</param>
    protected virtual void Set(TState state)
    {
        this._State = state;
        this._Subject.OnNext(this._State);
    }

    /// <summary>
    /// Patches the <see cref="ComponentStore{TState}"/>'s state
    /// </summary>
    /// <param name="reducer">A <see cref="Func{T, TResult}"/> used to reduce the <see cref="ComponentStore{TState}"/>'s state</param>
    protected virtual void Reduce(Func<TState, TState> reducer)
    {
        this.Set(reducer(this._State));
    }

    /// <inheritdoc/>
    public virtual IDisposable Subscribe(IObserver<TState> observer) => this._Subject.Subscribe(observer);

    /// <summary>
    /// Disposes of the <see cref="ComponentStore{TState}"/>
    /// </summary>
    /// <param name="disposing">A boolean indicating whether or not the <see cref="ComponentStore{TState}"/> is being disposed of</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!this._Disposed)
        {
            if (disposing)
            {
                this.CancellationTokenSource.Dispose();
                this._Subject.Dispose();
            }
            this._Disposed = true;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

}