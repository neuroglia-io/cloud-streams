namespace CloudStreams.Dashboard.StateManagement;

/// <summary>
/// Represents the delegate method used to dispatch an action
/// </summary>
/// <param name="context">The <see cref="IActionContext"/> in which to invoke the delegate</param>
/// <returns>The result of the dispatched action</returns>
public delegate Task<object> DispatchDelegate(IActionContext context);
