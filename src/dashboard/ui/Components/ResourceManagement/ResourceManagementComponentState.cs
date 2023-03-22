using CloudStreams.Core.Data.Models;

namespace CloudStreams.Dashboard.Components.ResourceManagement;

/// <summary>
/// Represents the state of a resource management component
/// </summary>
/// <typeparam name="TResource">The type of managed <see cref="IResource"/>s</typeparam>
public record ResourceManagementComponentState<TResource>
    where TResource : Resource, new()
{

    /// <summary>
    /// Gets the definition of the managed resource type
    /// </summary>
    public ResourceDefinition? Definition { get; set; }

    /// <summary>
    /// Gets a <see cref="List{T}"/> that contains all cached <see cref="IResource"/>s
    /// </summary>
    public List<TResource>? Resources { get; set; }

}
