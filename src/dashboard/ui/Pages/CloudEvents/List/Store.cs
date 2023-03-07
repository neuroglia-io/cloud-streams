using CloudStreams.Core.Api.Client.Services;
using CloudStreams.Dashboard.StateManagement;
using CloudStreams.Gateway.Api.Client.Services;
using System.Reactive.Linq;

namespace CloudStreams.Dashboard.Pages.CloudEvents.List;

/// <summary>
/// Represents the Cloud Event List's <see cref="ComponentStore{TState}"/>
/// </summary>
public class CloudEventListStore
    : ComponentStore<CloudEventListState>
{

    ICloudStreamsApiClient cloudStreamsApi;

    /// <summary>
    /// Initializes a new <see cref="CloudEventListStore"/>
    /// </summary>
    /// <param name="cloudStreamsApi">The service used to interact with the Cloud Streams Gateway API</param>
    public CloudEventListStore(ICloudStreamsApiClient cloudStreamsApi) 
        : base(new())
    { 
        this.cloudStreamsApi = cloudStreamsApi;
    }

    /// <summary>
    /// Gets an <see cref="IObservable{T}"/> used to observe <see cref="CloudEventListState.CloudEvents"/> changes
    /// </summary>
    public IObservable<List<CloudEvent>?> CloudEvents => this.Select(state => state.CloudEvents).DistinctUntilChanged();

    /// <summary>
    /// Gets an <see cref="IObservable{T}"/> used to observe <see cref="CloudEventListState.ReadOptions"/> changes
    /// </summary>
    private IObservable<StreamReadOptions> ReadOptions => this.Select(state => {
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
    }

    /// <summary>
    /// Sets the <see cref="StreamReadOptions"/>
    /// </summary>
    /// <param name="readOptions">The new <see cref="StreamReadOptions"/></param>
    public void SetReadOptions(StreamReadOptions readOptions)
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
    /// Gathers and sets the <see cref="CloudEventListState.CloudEvents"/> based on the provided <see cref="StreamReadOptions"/>
    /// </summary>
    /// <param name="readOptions">The <see cref="StreamReadOptions"/> to gather the <see cref="CloudEventListState.CloudEvents"/> with</param>
    /// <returns></returns>
    protected async Task SetCloudEventsAsync(StreamReadOptions readOptions)
    {
        if (readOptions == null) return;
        var cloudEvents = await (await this.cloudStreamsApi.CloudEvents.Stream.ReadStreamAsync(readOptions, this.CancellationTokenSource.Token).ConfigureAwait(false)).ToListAsync().ConfigureAwait(false);
        this.Reduce(state => state with
        {
            CloudEvents = cloudEvents!
        });
    }

}
