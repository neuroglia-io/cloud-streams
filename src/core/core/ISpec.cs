namespace CloudStreams.Core;

/// <summary>
/// Defines the fundamentals of an object defined by a spec
/// </summary>
public interface ISpec
{

    /// <summary>
    /// Gets the object's spec
    /// </summary>
    object Spec { get; }

}

/// <summary>
/// Defines the fundamentals of an object defined by a spec
/// </summary>
/// <typeparam name="TSpec">The type of spec</typeparam>
public interface ISpec<TSpec>
    : ISpec
    where TSpec : class, new()
{

    /// <summary>
    /// Gets the object's spec
    /// </summary>
    new TSpec Spec { get; }

}
