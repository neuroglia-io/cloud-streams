namespace CloudStreams.Dashboard.StateManagement;

/// <summary>
/// Represents the attribute used to mark a method or all the methods of a type as flux reducer
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public class ReducerAttribute
    : Attribute
{



}
