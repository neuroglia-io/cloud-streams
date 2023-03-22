using System.Reactive.Subjects;

namespace CloudStreams.Dashboard.StateManagement;

/// <summary>
/// Represents the default implementation of the <see cref="IDispatcher"/> interface
/// </summary>
public class Dispatcher
    : IDispatcher
{

    /// <summary>
    /// Gets the <see cref="Subject"/> used to stream actions
    /// </summary>
    protected Subject<object> Stream { get; } = new();

    /// <inheritdoc/>
    public void Dispatch(object action)
    {
        if(action == null)
            throw new ArgumentNullException(nameof(action));  
        this.Stream.OnNext(action);
    }

    /// <inheritdoc/>
    public virtual IDisposable Subscribe(IObserver<object> observer)
    {
        if (observer == null)
            throw new ArgumentNullException(nameof(observer));
        return this.Stream.Subscribe(observer);
    }

}
