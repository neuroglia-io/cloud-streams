using CloudStreams.Core.Infrastructure.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace CloudStreams.Core.Infrastructure.Services;

/// <summary>
/// Represents the base class for all services used to control <see cref="IResource"/>s
/// </summary>
/// <typeparam name="TResource">The type of <see cref="IResource"/> to control</typeparam>
public abstract class ResourceController<TResource>
    : BackgroundService
    where TResource : class, IResource, new()
{

    /// <summary>
    /// Initializes a new <see cref="ResourceController{TResource}"/>
    /// </summary>
    /// <param name="loggerFactory">The service used to create <see cref="ILogger"/>s</param>
    /// <param name="options">The service used to access the current <see cref="IOptions{TOptions}"/></param>
    /// <param name="resourceRepository">The service used to manage <see cref="IResource"/>s</param>
    public ResourceController(ILoggerFactory loggerFactory, IOptions<ResourceControllerOptions> options, IResourceRepository resourceRepository)
    {
        this.Logger = loggerFactory.CreateLogger(this.GetType());
        this.Options = options.Value;
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

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await this.ReconcileAsync(stoppingToken).ConfigureAwait(false);
        this.ResourceWatcher = await this.ResourceRepository.WatchResourcesAsync<TResource>(this.Options.ResourceNamespace, stoppingToken).ConfigureAwait(false);
        this.ResourceWatcher.SubscribeAsync(async e => await this.OnResourceChangedAsync(e, stoppingToken).ConfigureAwait(false), cancellationToken: stoppingToken);
        this.ReconciliationTimer = new(async _ => await this.ReconcileAsync(stoppingToken).ConfigureAwait(false), null, TimeSpan.Zero, this.Options.Reconciliation.Interval);
    }

    /// <summary>
    /// Reconciles the state of controlled resources with their actual state, as advertized by the server
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    protected abstract Task ReconcileAsync(CancellationToken cancellationToken = default);

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
    protected virtual Task OnResourceAddedAsync(TResource resource, CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <summary>
    /// Handles the update of a <see cref="IResource"/>
    /// </summary>
    /// <param name="resource">The updated <see cref="IResource"/></param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    protected virtual Task OnResourceUpdatedAsync(TResource resource, CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <summary>
    /// Handles the deletion of a <see cref="IResource"/>
    /// </summary>
    /// <param name="resource">The deleted <see cref="IResource"/></param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    protected virtual Task OnResourceDeletedAsync(TResource resource, CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <inheritdoc/>
    public override void Dispose()
    {
        this.ReconciliationTimer?.Dispose();
        this.ReconciliationTimer = null;
        base.Dispose();
        GC.SuppressFinalize(this);
    }

}