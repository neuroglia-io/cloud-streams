using CloudStreams.Core.Data.Models;
using CloudStreams.Dashboard.StateManagement;
using CloudStreams.Gateway.Api.Client.Services;
using System.Reactive.Linq;

namespace CloudStreams.Dashboard.Pages.CloudEvents.List;

/// <summary>
/// Reoresents the Cloud Event List's <see cref="ComponentStore{TState}"/>
/// </summary>
public class CloudEventListStore
    : ComponentStore<CloudEventListState>
{

    ICloudStreamsGatewayApiClient cloudStreamsGatewayApi;
    CloudEventStreamReadOptions readOptions;
    List<CloudEvent>? cloudEvents;
    IDisposable? cloudEventSubscription;

    /// <summary>
    /// Initializes a new <see cref="CloudEventListStore"/>
    /// </summary>
    /// <param name="cloudStreamsGatewayApi">The service used to interact with the Cloud Streams Gateway API</param>
    /// <param name="cloudEventHub">The service used to observe ingested cloud events</param>
    public CloudEventListStore(ICloudStreamsGatewayApiClient cloudStreamsGatewayApi, CloudEventHubClient cloudEventHub) 
        : base(new())
    { 
        this.cloudStreamsGatewayApi = cloudStreamsGatewayApi;
        this.ReadOptions.Subscribe(async o => await this.OnReadOptionsChangedAsync(o).ConfigureAwait(false), token: this.CancellationTokenSource.Token);
        this.CloudEvents.Subscribe(this.OnCloudEventCollectionChanged!, token: this.CancellationTokenSource.Token);
        this.readOptions = new(StreamReadDirection.Backwards);
        this.CloudEventHub = cloudEventHub;
        this.cloudEventSubscription = this.CloudEventHub.Subscribe(OnCloudEventIngested);
    }

    /// <summary>
    /// Gets the service used to observe ingested cloud events
    /// </summary>
    protected CloudEventHubClient CloudEventHub { get; }

    /// <summary>
    /// Gets an <see cref="IObservable{T}"/> used to observe <see cref="CloudEventListState.ReadOptions"/>-related changes
    /// </summary>
    public IObservable<CloudEventStreamReadOptions> ReadOptions => this.Select(s => s.ReadOptions).DistinctUntilChanged();

    /// <summary>
    /// Gets an <see cref="IObservable{T}"/> used to observe <see cref="CloudEventListState.CloudEvents"/>-related changes
    /// </summary>
    public IObservable<List<CloudEvent>?> CloudEvents => this.Select(s => s.CloudEvents);

    /// <inheritdoc/>
    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        await this.CloudEventHub.StartAsync();
        this.Reduce(state => state with { ReadOptions = this.readOptions });
    }

    /// <summary>
    /// Sets the current <see cref="CloudEventStreamReadOptions"/>
    /// </summary>
    /// <param name="reducer">A <see cref="Func{T, TResult}"/> used to reduce the current <see cref="CloudEventStreamReadOptions"/></param>
    public void ReduceStreamReadOptions(Func<CloudEventStreamReadOptions, CloudEventStreamReadOptions> reducer)
    {
        this.Reduce(state => state with
        {
            ReadOptions = reducer(state.ReadOptions)
        });
    }

    async Task ReadStreamAsync()
    {
        var cloudEvents = await (await this.cloudStreamsGatewayApi.CloudEvents.Stream.ReadStreamAsync(this.readOptions, this.CancellationTokenSource.Token).ConfigureAwait(false)).ToListAsync().ConfigureAwait(false);
        this.Reduce(state =>
        {
            return state with
            {
                CloudEvents = cloudEvents!
            };
        });
    }

    void OnCloudEventIngested(CloudEvent e)
    {
        if (this.cloudEvents == null) this.cloudEvents = new();
        if(this.readOptions.Direction == StreamReadDirection.Backwards) this.cloudEvents.Insert(0, e);
        else this.cloudEvents.Add(e);
        this.Reduce(state =>
        {
            return state with
            {
                CloudEvents = this.cloudEvents
            };
        });
    }

    Task OnReadOptionsChangedAsync(CloudEventStreamReadOptions readOptions)
    {
        this.readOptions = readOptions;
        if (this.readOptions.Partition != null && string.IsNullOrWhiteSpace(this.readOptions.Partition.Id)) return Task.CompletedTask;
        return this.ReadStreamAsync();
    }

    void OnCloudEventCollectionChanged(List<CloudEvent>? cloudEvents)
    {
        this.cloudEvents = cloudEvents;
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (!disposing) return;
        this.cloudEventSubscription?.Dispose();
        base.Dispose(disposing);
    }

}
