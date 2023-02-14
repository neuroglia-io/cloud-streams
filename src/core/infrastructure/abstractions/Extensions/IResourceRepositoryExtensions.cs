using CloudStreams.Infrastructure.Services;

namespace CloudStreams.Infrastructure;

/// <summary>
/// Defines extensions for <see cref="IResourceMonitor{TResource}"/> instances
/// </summary>
public static class IResourceRepositoryExtensions
{

    /// <summary>
    /// Creates a new <see cref="IResourceMonitor{TResource}"/> to monitor the state of the specified resource
    /// </summary>
    /// <typeparam name="TResource">The type of the resource to monitor</typeparam>
    /// <param name="repository">The extended <see cref="IResourceRepository"/></param>
    /// <param name="name">The name of the resource to monitor</param>
    /// <param name="namespace">The namespace the resource to monitor belongs to, if any</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IResourceMonitor{TResource}"/></returns>
    public static async Task<IResourceMonitor<TResource>> MonitorAsync<TResource>(this IResourceRepository repository, string name, string? @namespace = null, CancellationToken cancellationToken = default)
        where TResource : class, IResource, new()
    {
        var state = await repository.GetResourceAsync<TResource>(name, @namespace, cancellationToken);
        if (state == null) throw ApplicationException.ResourceNotFound<TResource>(name, @namespace);
        var monitor = new ResourceMonitor<TResource>(repository, state);
        await monitor.StartAsync(cancellationToken);
        return monitor;
    }

}
