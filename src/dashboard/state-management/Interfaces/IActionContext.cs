namespace CloudStreams.Dashboard.StateManagement;

/// <summary>
/// Defines the fundamentals of a Flux action context
/// </summary>
public interface IActionContext
{

    /// <summary>
    /// Gets the current <see cref="IServiceProvider"/>
    /// </summary>
    IServiceProvider Services { get; }

    /// <summary>
    /// Gets the current <see cref="IStore"/>
    /// </summary>
    IStore Store { get; }

    /// <summary>
    /// Gets/sets the Flux action to dispatch
    /// </summary>
    object Action { get; set; }

}