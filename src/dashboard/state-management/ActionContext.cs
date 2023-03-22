namespace CloudStreams.Dashboard.StateManagement;

/// <summary>
/// Represents the default implementation of the <see cref="IActionContext"/> interface
/// </summary>
public class ActionContext
    : IActionContext
{

    /// <summary>
    /// Initializes a new <see cref="ActionContext"/>
    /// </summary>
    /// <param name="services">The current <see cref="IServiceProvider"/></param>
    /// <param name="store">The current <see cref="IStore"/></param>
    /// <param name="action">The action to dispatch</param>
    public ActionContext(IServiceProvider services, IStore store, object action)
    {
        this.Services = services;
        this.Store = store;
        this.Action = action;
    }

    /// <inheritdoc/>
    public IServiceProvider Services { get; }

    /// <inheritdoc/>
    public IStore Store { get; }

    /// <inheritdoc/>
    public object Action { get; set; }

}
