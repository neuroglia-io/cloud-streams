using CloudStreams.Core.Infrastructure.Configuration;
using CloudStreams.Core.Infrastructure.Services;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace CloudStreamsBroker.Application.Services;

/// <summary>
/// Represents a service used to control <see cref="Channel"/> resources
/// </summary>
public class ChannelResourceController
    : ResourceController<Channel>
{

    /// <inheritdoc/>
    public ChannelResourceController(ILoggerFactory loggerFactory, IOptions<ResourceControllerOptions> options, IResourceRepository resourceRepository) : base(loggerFactory, options, resourceRepository) { }

    /// <summary>
    /// Gets a <see cref="ConcurrentDictionary{TKey, TValue}"/> containing all controlled <see cref="Channel"/>s
    /// </summary>
    protected ConcurrentDictionary<string, Channel> Channels { get; } = new();

    /// <inheritdoc/>
    protected override async Task ReconcileAsync(CancellationToken cancellationToken = default)
    {
        await foreach (var channel in (await this.ResourceRepository.ListResourcesAsync<Channel>(cancellationToken: cancellationToken).ConfigureAwait(false))!)
        {

        }
    }

    /// <inheritdoc/>
    protected override Task OnResourceAddedAsync(Channel resource, CancellationToken cancellationToken = default)
    {
        
    }

    /// <inheritdoc/>
    protected override Task OnResourceUpdatedAsync(Channel resource, CancellationToken cancellationToken = default)
    {
       
    }

    /// <inheritdoc/>
    protected override Task OnResourceDeletedAsync(Channel resource, CancellationToken cancellationToken = default)
    {
        
    }

}
