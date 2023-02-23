using CloudStreams.Dashboard.StateManagement;
using CloudStreams.ResourceManagement.Api.Client.Services;
using System.Reactive.Linq;

namespace CloudStreams.Dashboard.Components.ResourceManagement;

/// <summary>
/// Represents a <see cref="ComponentStore{TState}"/> used to manage Cloud Streams <see cref="IResource"/>s of the specified type
/// </summary>
/// <typeparam name="TResource">The type of <see cref="IResource"/>s to manage</typeparam>
public class ResourceManagementComponentStore<TResource>
    : ComponentStore<ResourceManagementComponentState<TResource>>
    where TResource : class, IResource, new()
{

    ICloudStreamsResourceManagementApiClient resourceManagementApi;
    ResourceDefinition? definition;
    List<TResource>? resources;

    /// <summary>
    /// Initializes a new <see cref="ResourceManagementComponentStore{TResource}"/>
    /// </summary>
    /// <param name="resourceManagementApi">The service used to interact with the Cloud Streams Resource management API</param>
    public ResourceManagementComponentStore(ICloudStreamsResourceManagementApiClient resourceManagementApi)
        : base(new())
    {
        this.resourceManagementApi = resourceManagementApi;
    }

    /// <summary>
    /// Gets an <see cref="IObservable{T}"/> used to observe <see cref="IResource"/>s of the specified type
    /// </summary>
    public IObservable<List<TResource>?> Resources => this.Select(s => s.Resources);

    /// <inheritdoc/>
    public override Task InitializeAsync() => base.InitializeAsync();

    /// <summary>
    /// Fetches the definition of the managed <see cref="IResource"/> type
    /// </summary>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    public virtual async Task GetResourceDefinitionAsync()
    {
        this.definition = await this.resourceManagementApi.Manage<TResource>().GetDefinitionAsync().ConfigureAwait(false);
        this.Reduce(s => s with
        {
            Definition = this.definition
        });
    }

    /// <summary>
    /// Lists all the channels managed by Cloud Streams
    /// </summary>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    public virtual async Task ListResourcesAsync()
    {
        this.resources = await (await this.resourceManagementApi.Manage<TResource>().ListAsync().ConfigureAwait(false)).ToListAsync().ConfigureAwait(false);
        this.Reduce(s => s with
        {
            Resources = this.resources
        });
    }

    /// <summary>
    /// Deletes the specified <see cref="IResource"/>
    /// </summary>
    /// <param name="resource">The <see cref="IResource"/> to delete</param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    public virtual async Task DeleteResourceAsync(TResource resource)
    {
        await this.resourceManagementApi.Manage<TResource>().DeleteAsync(resource.GetName(), resource.GetNamespace()).ConfigureAwait(false);
        var match = this.resources?.ToList().FirstOrDefault(r => r.GetName() == resource.GetName() && r.GetNamespace() == resource.GetNamespace());
        var resourceCollectionChanged = false;
        if (match != null)
        {
            this.resources!.Remove(match);
            resourceCollectionChanged = true;
        }
        if (!resourceCollectionChanged) return;
        this.Reduce(s => s with
        {
            Resources = this.resources
        });
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (!disposing) return;
        base.Dispose(disposing);
    }

}
