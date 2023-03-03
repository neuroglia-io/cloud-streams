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
    readonly ICloudStreamsResourceManagementApiClient resourceManagementApi;
    ResourceDefinition? definition;
    List<TResource>? resources;

    /// <summary>
    /// Initializes a new <see cref="ResourceManagementComponentStore{TResource}"/>
    /// </summary>
    /// <param name="resourceManagementApi">The service used to interact with the Cloud Streams Resource management API</param>
    /// <param name="resourceEventHub">The <see cref="IResourceEventWatchHub"/> websocket service client</param>
    public ResourceManagementComponentStore(ICloudStreamsResourceManagementApiClient resourceManagementApi, ResourceWatchEventHubClient resourceEventHub)
        : base(new())
    {
        this.resourceManagementApi = resourceManagementApi;
        this.ResourceEventHub = resourceEventHub;
    }

    /// <summary>
    /// Gets an <see cref="IObservable{T}"/> used to observe <see cref="IResource"/>s of the specified type
    /// </summary>
    public IObservable<List<TResource>?> Resources => this.Select(s => s.Resources);

    /// <summary>
    /// Gets the <see cref="IResourceEventWatchHub"/> websocket service client
    /// </summary>
    protected ResourceWatchEventHubClient ResourceEventHub { get; }

    /// <summary>
    /// Gets the service used to monitor resources of the specified type
    /// </summary>
    protected ResourceWatch<TResource> ResourceWatch { get; private set; } = null!;

    /// <summary>
    /// Gets an <see cref="IDisposable"/> that represents the store's <see cref="ResourceWatch"/> subscription
    /// </summary>
    protected IDisposable ResourceWatchSubscription { get; private set; } = null!;

    /// <inheritdoc/>
    public override async Task InitializeAsync()
    {
        await this.ResourceEventHub.StartAsync().ConfigureAwait(false);
        this.ResourceWatch = await this.ResourceEventHub.WatchAsync<TResource>().ConfigureAwait(false);
        this.ResourceWatch.SubscribeAsync(OnResourceWatchEventAsync, onErrorAsync: ex => Task.Run(() => Console.WriteLine(ex)));
        await base.InitializeAsync();
    }

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

    /// <summary>
    /// Handles the specified <see cref="IResourceWatchEvent"/>
    /// </summary>
    /// <param name="e">The <see cref="IResourceWatchEvent"/> to handle</param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    protected virtual Task OnResourceWatchEventAsync(IResourceWatchEvent<TResource> e)
    {
        Console.WriteLine($"EVENT RECEIVED: {e.Type}");
        switch (e.Type)
        {
            case ResourceWatchEventType.Created:
                this.Reduce(state =>
                {
                    List<TResource> resources = state.Resources == null ? new() : new(state.Resources);
                    resources.Add(e.Resource);
                    return state with
                    {
                        Resources = resources
                    };
                });
                break;
            case ResourceWatchEventType.Updated:
                this.Reduce(state =>
                {
                    List<TResource> resources = state.Resources == null ? new() : new(state.Resources);
                    var resource = resources.FirstOrDefault(r => r.GetQualifiedName() == e.Resource.GetQualifiedName());
                    if (resource == null) return state;
                    var index = resources.IndexOf(resource);
                    resources.Remove(resource);
                    resources.Insert(index, e.Resource);
                    return state with
                    {
                        Resources = resources
                    };
                });
                break;
            case ResourceWatchEventType.Deleted:
                this.Reduce(state =>
                {
                    List<TResource> resources = state.Resources == null ? new() : new(state.Resources);
                    var resource = resources.FirstOrDefault(r => r.GetQualifiedName() == e.Resource.GetQualifiedName());
                    if (resource == null) return state;
                    resources.Remove(resource);
                    return state with
                    {
                        Resources = resources
                    };
                });
                break;
            default:
                throw new NotSupportedException($"The specified {nameof(ResourceWatchEventType)} '{e.Type}' is not supported");
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (!disposing) return;
        this.ResourceWatchSubscription?.Dispose();
        base.Dispose(disposing);
    }

    /// <inheritdoc/>
    protected override async ValueTask DisposeAsync(bool disposing)
    {
        if (!disposing) return;
        await this.ResourceWatch.DisposeAsync().ConfigureAwait(false);
        this.ResourceWatchSubscription.Dispose();
        base.Dispose(disposing);
    }

}
