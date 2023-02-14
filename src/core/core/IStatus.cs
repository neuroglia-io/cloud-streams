namespace CloudStreams;

/// <summary>
/// Defines the fundamentals of an object which's status is described by a dedicated object
/// </summary>
public interface IStatus
{

    /// <summary>
    /// Gets the object's status
    /// </summary>
    object? Status { get; }

}

/// <summary>
/// Defines the fundamentals of an object which's status is described by a dedicated object
/// </summary>
/// <typeparam name="TStatus"></typeparam>
public interface IStatus<TStatus>
    : IStatus
    where TStatus : class, new()
{

    /// <summary>
    /// Gets the object's status
    /// </summary>
    new TStatus? Status { get; }

}
