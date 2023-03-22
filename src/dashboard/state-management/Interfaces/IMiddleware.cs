namespace CloudStreams.Dashboard.StateManagement;

/// <summary>
/// Defines the fundamentals of a middleware
/// </summary>
public interface IMiddleware
{

    /// <summary>
    /// Invokes the <see cref="IMiddleware"/>
    /// </summary>
    /// <param name="context">The <see cref="IActionContext"/> in which to invoke the <see cref="IMiddleware"/></param>
    /// <returns>The result of the dispatched Flux action</returns>
    Task<object> InvokeAsync(IActionContext context);

}