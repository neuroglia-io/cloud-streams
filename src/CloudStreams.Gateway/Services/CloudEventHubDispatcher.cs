using CloudStreams.Core.Infrastructure;
using CloudStreams.Core.Infrastructure.Services;
using Microsoft.AspNetCore.SignalR;
using Neuroglia.Data.Infrastructure.EventSourcing;
using Neuroglia.Reactive;
using System.Reactive.Linq;

namespace CloudStreams.Gateway.Services;

/// <summary>
/// Represents a service used to dispatch ingested cloud events to all <see cref="ICloudEventHub"/>s
/// </summary>
/// <remarks>
/// Initializes a new <see cref="CloudEventHubDispatcher"/>
/// </remarks>
/// <param name="serviceProvider">The current <see cref="IServiceProvider"/></param>
/// <param name="logger">The service used to perform logging</param>
/// <param name="hubContext">The current <see cref="ICloudEventHubClient"/>'s <see cref="IHubContext{THub, T}"/></param>
public class CloudEventHubDispatcher(IServiceProvider serviceProvider, ILogger<CloudEventHubDispatcher> logger, IHubContext<CloudEventHub, ICloudEventHubClient> hubContext)
    : BackgroundService
{

    /// <summary>
    /// Gets the current <see cref="IServiceProvider"/>
    /// </summary>
    protected IServiceProvider ServiceProvider { get; } = serviceProvider;

    /// <summary>
    /// Gets the service used to perform logging
    /// </summary>
    protected ILogger Logger { get; } = logger;

    /// <summary>
    /// Gets the service used to source <see cref="CloudEvent"/>s
    /// </summary>
    protected ICloudEventStore EventStore => this.ServiceProvider.GetRequiredService<ICloudEventStore>();

    /// <summary>
    /// Gets the current <see cref="ICloudEventHubClient"/>'s <see cref="IHubContext{THub, T}"/>
    /// </summary>
    protected IHubContext<CloudEventHub, ICloudEventHubClient> HubContext { get; } = hubContext;

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                (await this.EventStore
                    .ObserveAsync(cancellationToken: stoppingToken))
                    .Select(e => e.ToCloudEvent(default))
                    .SubscribeAsync(e => this.HubContext.Clients.All.StreamEvent(e, stoppingToken), cancellationToken: stoppingToken);
                break;
            }
            catch (StreamNotFoundException)
            {
                var delay = 3000;
                this.Logger.LogWarning("Failed to observe the cloud event stream because the first cloud event is yet to be published. Retrying in {delay} milliseconds...", delay);
                await Task.Delay(delay, stoppingToken).ConfigureAwait(false);
            }
        }

    }

}
