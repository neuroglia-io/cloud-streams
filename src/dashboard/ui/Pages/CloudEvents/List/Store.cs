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
    /// Gets an <see cref="IObservable{T}"/> used to observe <see cref="CloudEventListState.ReadOptions"/>-related changes
    /// </summary>
    public IObservable<CloudEventStreamReadOptions> ReadOptions => this.Select(state => state.ReadOptions).DistinctUntilChanged();

    /// <summary>
    /// Gets an <see cref="IObservable{T}"/> used to observe <see cref="CloudEventListState.CloudEvents"/>-related changes
    /// </summary>
    public IObservable<List<CloudEvent>?> CloudEvents => this.Select(state => state.CloudEvents).DistinctUntilChanged();

    /// <summary>
    /// Gets an <see cref="IObservable{T}"/> used to observe <see cref="CloudEventListState.Partitions"/>-related changes
    /// </summary>
    public IObservable<List<CloudEventPartitionMetadata>?> Partitions => this.Select(state => state.Partitions).DistinctUntilChanged();

    /// <summary>
    /// Gets an <see cref="IObservable{T}"/> used to observe a sanitized version of the <see cref="CloudEventListState.ReadOptions"/>
    /// </summary>
    public IObservable<CloudEventStreamReadOptions> SanitizedReadOptions => this.ReadOptions.Select(options => {
        if (options.Partition?.Type != null && options.Partition?.Id == null)
        {
            options = options with
            {
                Partition = null
            };
        }
        return options;
    }).DistinctUntilChanged();

    /// <summary>
    /// Gets an <see cref="IObservable{T}"/> used to observe <see cref="CloudEventPartitionRef"/> of the current <see cref="CloudEventListState.ReadOptions"/>
    /// </summary>
    public IObservable<CloudEventPartitionRef?> Partition => this.ReadOptions.Select(options => options.Partition).DistinctUntilChanged();

    /// <summary>
    /// Gets an <see cref="IObservable{T}"/> used to observe <see cref="CloudEventPartitionType"/> of the current <see cref="CloudEventPartitionRef"/>
    /// </summary>
    public IObservable<CloudEventPartitionType?> PartitionType => this.Partition.Select(partition => partition?.Type).DistinctUntilChanged();

    /// <inheritdoc/>
    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        this.SanitizedReadOptions.SubscribeAsync(this.SetCloudEventsAsync, cancellationToken: this.CancellationTokenSource.Token);
        this.PartitionType.SubscribeAsync(this.SetPartitionsAsync, cancellationToken: this.CancellationTokenSource.Token);
        this.cloudEventHub.SelectAll().Subscribe(OnCloudEventIngested, token: this.CancellationTokenSource.Token);
        await this.cloudEventHub.StartAsync();
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

    /// <summary>
    /// Gathers and sets the <see cref="CloudEventListState.Partitions"/> based on the provided <see cref="CloudEventPartitionType"/>
    /// </summary>
    /// <param name="partitionType">The <see cref="CloudEventPartitionType"/> to gather the <see cref="CloudEventListState.Partitions"/> with</param>
    /// <returns></returns>
    protected async Task SetPartitionsAsync(CloudEventPartitionType? partitionType)
    {
        if (!partitionType.HasValue)
        {
            this.Reduce(state => state with
            {
                Partitions = null
            });
            return;
        }
        var partitions = await (await this.cloudStreamsGatewayApi.CloudEvents.Partitions.ListPartitionsByTypeAsync(partitionType.Value, this.CancellationTokenSource.Token).ConfigureAwait(false)).ToListAsync().ConfigureAwait(false);
        this.Reduce(state => state with
        {
            Partitions = partitions!
        });
    }

}
