using CloudStreams.ResourceManagement.Api.Client.Services;
using Microsoft.AspNetCore.SignalR;

namespace CloudStreams.ResourceManagement.Api.Hubs;

/// <summary>
/// Represents the <see cref="Hub"/> used to notify clients about resource-related changes
/// </summary>
public class ResourceEventWatchHub
    : Hub<IResourceEventWatchHubClient>, IResourceEventWatchHub
{

    /// <inheritdoc/>
    public virtual async Task Subscribe(string type, string? @namespace = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(type)) throw new ArgumentNullException(nameof(type));
    }

    /// <inheritdoc/>
    public virtual async Task Unsubscribe(string type, string? @namespace = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(type)) throw new ArgumentNullException(nameof(type));
    }

    /// <summary>
    /// Gets the hub's group key for the specified resource type and namespace
    /// </summary>
    /// <param name="type">The type of resource to get the group key for</param>
    /// <param name="namespace">The namespace the resources to watch belong to, if any</param>
    /// <returns>The hub's group key for the specified resource type and namespace</returns>
    protected virtual string GetGroupKey(string type, string? @namespace)
    {
        if (string.IsNullOrWhiteSpace(type)) throw new ArgumentNullException(nameof(type));
        if (string.IsNullOrWhiteSpace(@namespace)) return type;
        else return $"{type}/{@namespace}";
    }

}
