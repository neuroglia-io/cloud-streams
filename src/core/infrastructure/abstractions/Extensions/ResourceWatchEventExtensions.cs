namespace CloudStreams.Core.Infrastructure;

/// <summary>
/// Defines extensions for <see cref="IResourceWatchEvent"/>s
/// </summary>
public static class IResourceWatchEventExtensions
{

    /// <summary>
    /// Converts the <see cref="IResourceWatchEvent"/> into a new <see cref="IResourceWatchEvent{TResource}"/>
    /// </summary>
    /// <typeparam name="TResource">The type of watched <see cref="IResource"/>s</typeparam>
    /// <param name="e">The <see cref="IResourceWatchEvent"/> to convert</param>
    /// <returns>A new <see cref="IResourceWatchEvent{TResource}"/></returns>
    public static IResourceWatchEvent<TResource> ToType<TResource>(this IResourceWatchEvent e)
        where TResource : class, IResource, new()
    {
        var resource = Serializer.Json.Deserialize<TResource>(Serializer.Json.Serialize(e.Resource))!;
        return new ResourceWatchEvent<TResource>(e.Type, resource);
    }

}
