using CloudStreams.Dashboard.StateManagement;
using CloudStreams.Gateway.Api.Client.Services;
using Microsoft.Extensions.Options;
using System.Reactive.Linq;

namespace CloudStreams.Dashboard.Pages.CloudEvents.List;

/// <summary>
/// Represents the Cloud Event List's <see cref="ComponentStore{TState}"/>
/// </summary>
public class CloudEventListStore
    : ComponentStore<CloudEventListState>
{
    /// <summary>
    /// The service used to interact with the Cloud Streams Gateway API
    /// </summary>
    private ICloudStreamsGatewayApiClient cloudStreamsGatewayApi;

    /// <summary>
    /// Gets the service used to observe ingested cloud events
    /// </summary>
    private CloudEventHubClient cloudEventHub;

    /// <summary>
    /// Initializes a new <see cref="CloudEventListStore"/>
    /// </summary>
    /// <param name="cloudStreamsGatewayApi">The service used to interact with the Cloud Streams Gateway API</param>
    /// <param name="cloudEventHub">The service used to observe ingested cloud events</param>
    public CloudEventListStore(ICloudStreamsGatewayApiClient cloudStreamsGatewayApi, CloudEventHubClient cloudEventHub)
        : base(new())
    {
        this.cloudStreamsGatewayApi = cloudStreamsGatewayApi;
        this.cloudEventHub = cloudEventHub;
    }

    /// <summary>
    /// Gets an <see cref="IObservable{T}"/> used to observe <see cref="CloudEventListState.CloudEvents"/> changes
    /// </summary>
    public IObservable<List<CloudEvent>?> CloudEvents => this.Select(state => state.CloudEvents).DistinctUntilChanged();

    /// <summary>
    /// Gets an <see cref="IObservable{T}"/> used to observe <see cref="CloudEventListState.ReadOptions"/> changes
    /// </summary>
    private IObservable<CloudEventStreamReadOptions> ReadOptions => this.Select(state => {
        if (state.ReadOptions.Partition?.Type != null && state.ReadOptions.Partition?.Id != null)
        {
            return state.ReadOptions;
        }
        return state.ReadOptions with
        {
            Partition = null
        };
    }).DistinctUntilChanged();

    /// <inheritdoc/>
    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        this.ReadOptions.SubscribeAsync(this.SetCloudEventsAsync, cancellationToken: this.CancellationTokenSource.Token);
        this.cloudEventHub.SelectAll().Subscribe(OnCloudEventIngested, token: this.CancellationTokenSource.Token);
        await this.cloudEventHub.StartAsync();
    }

    /// <summary>
    /// Sets the <see cref="CloudEventStreamReadOptions"/>
    /// </summary>
    /// <param name="readOptions">The new <see cref="CloudEventStreamReadOptions"/></param>
    public void SetReadOptions(CloudEventStreamReadOptions readOptions)
    {
        this.Reduce(state => state with
        {
            ReadOptions = readOptions
        });
    }

    /// <summary>
    /// Updates the <see cref="CloudEventListState.CloudEvents"/> when the <see cref="CloudEventHubClient"/> emits a new values
    /// </summary>
    /// <param name="e"></param>
    protected void OnCloudEventIngested(CloudEvent e)
    {
        List<CloudEvent> cloudEvents = new(this.Get(state => state.CloudEvents) ?? new());
        if (this.Get(state => state.ReadOptions).Direction == StreamReadDirection.Backwards) cloudEvents.Insert(0, e);
        else cloudEvents.Add(e);
        this.Reduce(state =>
        {
            return state with
            {
                CloudEvents = cloudEvents
            };
        });
    }

    /// <summary>
    /// Gathers and sets the <see cref="CloudEventListState.CloudEvents"/> based on the provided <see cref="CloudEventStreamReadOptions"/>
    /// </summary>
    /// <param name="readOptions">The <see cref="CloudEventStreamReadOptions"/> to gather the <see cref="CloudEventListState.CloudEvents"/> with</param>
    /// <returns></returns>
    protected async Task SetCloudEventsAsync(CloudEventStreamReadOptions readOptions)
    {
        if (readOptions == null) return;
        var cloudEvents = await (await this.cloudStreamsGatewayApi.CloudEvents.Stream.ReadStreamAsync(readOptions, this.CancellationTokenSource.Token).ConfigureAwait(false)).ToListAsync().ConfigureAwait(false);
        this.Reduce(state => state with
        {
            CloudEvents = cloudEvents!
        });
    }

}
