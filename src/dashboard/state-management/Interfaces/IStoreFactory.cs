namespace CloudStreams.Dashboard.StateManagement;


/// <summary>
/// Defines the fundamentals of a service used to create <see cref="IStore"/>s
/// </summary>
public interface IStoreFactory
{

    /// <summary>
    /// Creates a new <see cref="IStore"/>
    /// </summary>
    /// <returns>A new <see cref="IStore"/></returns>
    IStore CreateStore();

}
