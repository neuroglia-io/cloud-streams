using CloudStreams.Core.Infrastructure.Services;
using k8s;
using System.Text.Json;

namespace CloudStreams.Core.Infrastructure;

/// <summary>
/// Defines extensions for <see cref="Kubernetes"/> instances
/// </summary>
public static class KubernetesExtensions
{

    /// <summary>
    /// Lists <see cref="IResource"/>s of the specified type
    /// </summary>
    /// <typeparam name="TResource">The type of <see cref="IResource"/> to list</typeparam>
    /// <param name="kubernetes">The extended <see cref="Kubernetes"/></param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="CustomResourceList{T}"/>, that wraps the query results</returns>
    public static async Task<CustomResourceList<TResource>?> ListClusterCustomObjectAsync<TResource>(this Kubernetes kubernetes, CancellationToken cancellationToken = default)
        where TResource : class, IResource, new()
    {
        var resource = new TResource();
        var group = resource.Type.Group;
        var version = resource.Type.Version;
        var plural = resource.Type.Plural;
        JsonElement? resourceObjectArray;
        resourceObjectArray = (JsonElement)await kubernetes.CustomObjects.ListClusterCustomObjectAsync(group, version, plural, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (resourceObjectArray == null) return null;
        return Serializer.Json.Deserialize<CustomResourceList<TResource>>((JsonElement)resourceObjectArray);
    }

}
