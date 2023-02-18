namespace CloudStreams.Dashboard.StateManagement;

/// <summary>
/// Represents the default implementation of the <see cref="IEffectContext"/> interface
/// </summary>
public class EffectContext
    : IEffectContext
{

    /// <summary>
    /// Initializes a new <see cref="EffectContext"/>
    /// </summary>
    /// <param name="services">The current <see cref="IServiceProvider"/></param>
    /// <param name="dispatcher">The current <see cref="IDispatcher"/></param>
    public EffectContext(IServiceProvider services, IDispatcher dispatcher)
    {
        this.Services = services;
        this.Dispatcher = dispatcher;
    }

    /// <inheritdoc/>
    public IServiceProvider Services { get; }

    /// <inheritdoc/>
    public IDispatcher Dispatcher { get; }

}
