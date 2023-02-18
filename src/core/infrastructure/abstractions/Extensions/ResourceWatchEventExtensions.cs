namespace CloudStreams.Core.Infrastructure;

/// <summary>
/// Defines extensions for <see cref="ResourceWatchEvent"/>s
/// </summary>
public static class ResourceWatchEventExtensions
{

    /// <summary>
    /// Converts the <see cref="ResourceWatchEvent"/> into a new <see cref="ResourceWatchEvent{TResource}"/>
    /// </summary>
    /// <typeparam name="TResource">The type of watched <see cref="IResource"/>s</typeparam>
    /// <param name="e">The <see cref="ResourceWatchEvent"/> to convert</param>
    /// <returns>A new <see cref="ResourceWatchEvent{TResource}"/></returns>
    public static ResourceWatchEvent<TResource> ToType<TResource>(this ResourceWatchEvent e)
        where TResource : class, IResource, new()
    {
        var resource = Serializer.Json.Deserialize<TResource>(Serializer.Json.Serialize(e.Resource))!;
        return new ResourceWatchEvent<TResource>(e.Type, resource);
    }

}