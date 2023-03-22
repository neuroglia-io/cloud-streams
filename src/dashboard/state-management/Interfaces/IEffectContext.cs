namespace CloudStreams.Dashboard.StateManagement;

/// <summary>
/// Defines the fundamentals of an <see cref="IEffect"/> context
/// </summary>
public interface IEffectContext
{

    /// <summary>
    /// Gets the current <see cref="IServiceProvider"/>
    /// </summary>
    IServiceProvider Services { get; }

    /// <summary>
    /// Gets the current <see cref="IDispatcher"/>
    /// </summary>
    IDispatcher Dispatcher { get; }

}
