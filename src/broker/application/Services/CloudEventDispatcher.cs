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
    protected ICloudEventStore Store { get; }

    protected IObservable<CloudEvent> Stream { get; private set; }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        this.Stream = await this.Store.SubscribeAsync(cancellationToken: stoppingToken).ConfigureAwait(false);
    }

    protected async Task OnCloudEventAsync(CloudEvent e)
    {
        
    }

}
