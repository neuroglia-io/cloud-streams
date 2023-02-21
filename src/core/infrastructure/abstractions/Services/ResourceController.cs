using CloudStreams.Core.Infrastructure.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace CloudStreams.Core.Infrastructure.Services;

/// <summary>
/// Represents the default implementation of the <see cref="IResourceController{TResource}"/> interface
/// </summary>
/// <typeparam name="TResource">The type of <see cref="IResource"/> to control</typeparam>
public class ResourceController<TResource>
    : BackgroundService, IResourceController<TResource>
    where TResource : class, IResource, new()
{

    /// <summary>
    /// Initializes a new <see cref="ResourceController{TResource}"/>
    /// </summary>
    /// <param name="loggerFactory">The service used to create <see cref="ILogger"/>s</param>
    /// <param name="controllerOptions">The service used to access the current <see cref="IOptions{TOptions}"/></param>
    /// <param name="resourceRepository">The service used to manage <see cref="IResource"/>s</param>
    public ResourceController(ILoggerFactory loggerFactory, IOptions<ResourceControllerOptions> controllerOptions, IResourceRepository resourceRepository)
    {
        this.Logger = loggerFactory.CreateLogger(this.GetType());
        this.Options = controllerOptions.Value;
        this.ResourceRepository = resourceRepository;
    }

    /// <summary>
    /// Gets the service used to perform logging
    /// </summary>
    protected ILogger Logger { get; }

    /// <summary>
    /// Gets the options used to configure the <see cref="ResourceController{TResource}"/>
    /// </summary>
    protected ResourceControllerOptions Options { get; }

    /// <summary>
    /// Gets the service used to manage <see cref="IResource"/>s
    /// </summary>
    protected IResourceRepository ResourceRepository { get; }

    /// <summary>
    /// Gets the service used to watch changes on <see cref="IResource"/>s to control
    /// </summary>
    protected IObservable<IResourceWatchEvent<TResource>>? ResourceWatcher { get; private set; }

    /// <summary>
    /// Gets the <see cref="System.Threading.Timer"/> used by the reconciliation loop
    /// </summary>
    protected Timer? ReconciliationTimer { get; private set; }

    /// <summary>
    /// Gets a <see cref="ConcurrentDictionary{TKey, TValue}"/> containing all controlled <see cref="IResource"/>s
    /// </summary>
    protected ConcurrentDictionary<string, TResource> Resources { get; } = new();

    List<TResource> IResourceController<TResource>.Resources => this.Resources.Values.ToList();

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await this.ReconcileAsync(stoppingToken).ConfigureAwait(false);
        this.ResourceWatcher = await this.ResourceRepository.WatchResourcesAsync<TResource>(this.Options.ResourceNamespace, cancellationToken: stoppingToken).ConfigureAwait(false);
        this.ResourceWatcher.SubscribeAsync(async e => await this.OnResourceChangedAsync(e, stoppingToken).ConfigureAwait(false), cancellationToken: stoppingToken);
        this.ReconciliationTimer = new(async _ => await this.ReconcileAsync(stoppingToken).ConfigureAwait(false), null, this.Options.Reconciliation.Interval, this.Options.Reconciliation.Interval);
    }

    /// <summary>
    /// Reconciles the state of controlled resources with their actual state, as advertized by the server
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    protected virtual async Task ReconcileAsync(CancellationToken cancellationToken = default)
    {
        var existingResourceKeys = new HashSet<string>();
        await foreach (var resource in (await this.ResourceRepository.ListResourcesAsync<TResource>(this.Options.ResourceNamespace, this.Options.LabelSelectors, cancellationToken).ConfigureAwait(false))!)
        {
            var cacheKey = this.GetResourceCacheKey(resource.GetName(), resource.GetNamespace());
            if (this.Resources.TryGetValue(cacheKey, out var cachedState) && cachedState != null)
            {
                if (cachedState.Metadata.ResourceVersion == resource.Metadata.ResourceVersion) continue;
                await this.OnResourceUpdatedAsync(cachedState, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await this.OnResourceAddedAsync(resource, cancellationToken).ConfigureAwait(false);
            }
            existingResourceKeys.Add(cacheKey);
        }
        foreach (var resource in this.Resources.ToList().Where(kvp => !existingResourceKeys.Contains(kvp.Key)).Select(kvp => kvp.Value))
        {
            await this.OnResourceDeletedAsync(resource, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Builds a new cache key for the specified resource
    /// </summary>
    /// <param name="name">The name of the resource to build a new cache key for</param>
    /// <param name="namespace">The namespace the resource to build a new cache key for belongs to</param>
    /// <returns>A new cache key</returns>
    protected virtual string GetResourceCacheKey(string name, string? @namespace) => string.IsNullOrWhiteSpace(@namespace) ? name : $"{@namespace}.{name}";

    /// <inheritdoc/>
    public virtual IDisposable Subscribe(IObserver<IResourceWatchEvent<TResource>> observer) => this.ResourceWatcher== null ? throw new Exception("The resource controller has not been properly initialized") : this.ResourceWatcher.Subscribe(observer);

    /// <summary>
    /// Handles the specified <see cref="IResource"/>'s change
    /// </summary>
    /// <param name="e">An <see cref="IResourceWatchEvent{TResource}"/> that describes the change that has occured</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    private Task OnResourceChangedAsync(IResourceWatchEvent<TResource> e, CancellationToken cancellationToken = default)
    {
        return e.Type switch
        {
            ResourceWatchEventType.Created => this.OnResourceAddedAsync(e.Resource, cancellationToken),
            ResourceWatchEventType.Updated => this.OnResourceUpdatedAsync(e.Resource, cancellationToken),
            ResourceWatchEventType.Deleted => this.OnResourceDeletedAsync(e.Resource, cancellationToken),
            _ => Task.CompletedTask
        };
    }

    /// <summary>
    /// Handles the creation of a new <see cref="IResource"/>
    /// </summary>
    /// <param name="resource">The newly created <see cref="IResource"/></param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    protected virtual Task OnResourceAddedAsync(TResource resource, CancellationToken cancellationToken = default)
    {
        var cacheKey = this.GetResourceCacheKey(resource.GetName(), resource.GetNamespace());
        resource = this.Resources.AddOrUpdate(cacheKey, resource, (key, existing) => resource);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles the update of a <see cref="IResource"/>
    /// </summary>
    /// <param name="resource">The updated <see cref="IResource"/></param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    protected virtual Task OnResourceUpdatedAsync(TResource resource, CancellationToken cancellationToken = default)
    {
        var cacheKey = this.GetResourceCacheKey(resource.GetName(), resource.GetNamespace());
        resource = this.Resources.AddOrUpdate(cacheKey, resource, (key, existing) => resource);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles the deletion of a <see cref="IResource"/>
    /// </summary>
    /// <param name="resource">The deleted <see cref="IResource"/></param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    protected virtual Task OnResourceDeletedAsync(TResource resource, CancellationToken cancellationToken = default)
    {
        var cacheKey = this.GetResourceCacheKey(resource.GetName(), resource.GetNamespace());
        this.Resources.Remove(cacheKey, out _);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        this.ReconciliationTimer?.Dispose();
        this.ReconciliationTimer = null;
        base.Dispose();
        GC.SuppressFinalize(this);
    }

}
