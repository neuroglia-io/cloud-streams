using CloudStreams.ResourceManagement.Api.Client.Services;

namespace CloudStreams;

/// <summary>
/// Defines extensions for <see cref="ICloudStreamsResourceManagementApiClient"/>s
/// </summary>
public static class ICloudStreamsResourceManagementApiClientExtensions
{

    /// <summary>
    /// Gets the <see cref="IResourceManagementApi{TResource}"/> for the specified <see cref="IResource"/> type
    /// </summary>
    /// <typeparam name="TResource">The type of <see cref="IResource"/> to get the <see cref="IResourceManagementApi{TResource}"/> for</typeparam>
    /// <param name="client">The extended <see cref="ICloudStreamsResourceManagementApiClient"/></param>
    /// <returns>The <see cref="IResourceManagementApi{TResource}"/> for the specified <see cref="IResource"/> type</returns>
    public static IResourceManagementApi<TResource> Manage<TResource>(this ICloudStreamsResourceManagementApiClient client)
        where TResource : class, IResource, new()
    {
        var apiProperty = client.GetType().GetProperties().SingleOrDefault(p => p.CanRead && typeof(IResourceManagementApi<>).MakeGenericType(typeof(TResource)).IsAssignableFrom(p.PropertyType));
        if (apiProperty == null) throw new NullReferenceException($"Failed to find a management API for the specified resource type '{new TResource().Type}'");
        return (IResourceManagementApi<TResource>)apiProperty.GetValue(client)!;
    }

}