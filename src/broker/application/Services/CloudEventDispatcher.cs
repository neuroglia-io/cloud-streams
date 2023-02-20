using CloudStreams.Core.Infrastructure.Services;

namespace CloudStreamsBroker.Application.Services;

/// <summary>
/// Represents the service used to dispatch consumed <see cref="CloudEvent"/>s to available <see cref="Channel"/>s
/// </summary>
public class CloudEventDispatcher
    : BackgroundService
{

    /// <summary>
    /// Gets the service used to manage <see cref="CloudEvent"/>s
    /// </summary>
    protected ICloudEventStore CloudEventStore { get; }

    protected IObservable<CloudEvent> CloudEventStream { get; private set; }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        this.CloudEventStream = await this.CloudEventStore.SubscribeAsync(cancellationToken: stoppingToken).ConfigureAwait(false);
    }

    protected async Task OnCloudEventAsync(CloudEvent e)
    {
        
    }

}
