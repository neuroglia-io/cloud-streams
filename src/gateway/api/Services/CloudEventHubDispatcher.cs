using CloudStreams.Core.Infrastructure.Services;
using CloudStreams.Gateway.Api.Client.Services;
using CloudStreams.Gateway.Api.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace CloudStreams.Gateway.Api.Services;

/// <summary>
/// Represents a service used to dispatch ingested cloud events to all <see cref="ICloudEventHub"/>s
/// </summary>
public class CloudEventHubDispatcher
    : BackgroundService
{

    /// <summary>
    /// Initializes a new <see cref="CloudEventHubDispatcher"/>
    /// </summary>
    /// <param name="eventStore">The service used to store <see cref="CloudEvent"/>s</param>
    /// <param name="hubContext">The current <see cref="ICloudEventHubClient"/>'s <see cref="IHubContext{THub, T}"/></param>
    public CloudEventHubDispatcher(ICloudEventStore eventStore, IHubContext<CloudEventHub, ICloudEventHubClient> hubContext)
    {
        this.EventStore = eventStore;
        this.HubContext = hubContext;
    }

    /// <summary>
    /// Gets the service used to store <see cref="CloudEvent"/>s
    /// </summary>
    protected ICloudEventStore EventStore { get; }

    /// <summary>
    /// Gets the current <see cref="ICloudEventHubClient"/>'s <see cref="IHubContext{THub, T}"/>
    /// </summary>
    protected IHubContext<CloudEventHub, ICloudEventHubClient> HubContext { get; }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        (await this.EventStore.SubscribeAsync(cancellationToken: stoppingToken).ConfigureAwait(false))
            .SubscribeAsync(e => this.HubContext.Clients.All.StreamEvent(e, stoppingToken), cancellationToken: stoppingToken);
    }

}
