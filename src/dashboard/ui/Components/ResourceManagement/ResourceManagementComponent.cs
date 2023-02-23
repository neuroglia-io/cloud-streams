using CloudStreams.Dashboard.Components.ResourceManagement;

namespace CloudStreams.Dashboard.Components;

/// <summary>
/// Represents the base class for all components used to manage <see cref="IResource"/>s
/// </summary>
/// <typeparam name="TResource">The type of <see cref="IResource"/> to manage</typeparam>
public abstract class ResourceManagementComponent<TResource>
    : StatefulComponent<ResourceManagementComponentStore<TResource>, ResourceManagementComponentState<TResource>>
    where TResource : class, IResource, new()
{



}