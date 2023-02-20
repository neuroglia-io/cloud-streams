using CloudStreams.Gateway.Api.Client.Services;
using CloudStreams.Dashboard.StateManagement;
using CloudStreams.Core.Data.Models;
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

    /// <summary>
    /// Initializes a new <see cref="CloudEventListStore"/>
    /// </summary>
    /// <param name="cloudStreamsGatewayApi">The service used to interact with the Cloud Streams Gateway API</param>
    public CloudEventListStore(ICloudStreamsGatewayApiClient cloudStreamsGatewayApi) 
        : base(new())
    { 
        this.cloudStreamsGatewayApi = cloudStreamsGatewayApi;
        this.ReadOptions.Subscribe(async o => await this.OnReadOptionsChangedAsync(o).ConfigureAwait(false), token: this.CancellationTokenSource.Token);
        this.readOptions = new(StreamReadDirection.Backwards);
        this.Reduce(state => state with { ReadOptions = this.readOptions });
    }

    /// <summary>
    /// Gets an <see cref="IObservable{T}"/> used to observe <see cref="CloudEventListState.ReadOptions"/>-related changes
    /// </summary>
    public IObservable<CloudEventStreamReadOptions> ReadOptions => this.Select(s => s.ReadOptions).DistinctUntilChanged();

    /// <summary>
    /// Gets an <see cref="IObservable{T}"/> used to observe <see cref="CloudEventListState.CloudEvents"/>-related changes
    /// </summary>
    public IObservable<IAsyncEnumerable<CloudEvent?>?> CloudEvents => this.Select(s => s.CloudEvents).DistinctUntilChanged();

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
        var cloudEvents = await this.cloudStreamsGatewayApi.CloudEvents.Stream.ReadStreamAsync(this.readOptions, this.CancellationTokenSource.Token).ConfigureAwait(false);
        this.Reduce(state =>
        {
            return state with
            {
                CloudEvents = cloudEvents
            };
        });
    }

    Task OnReadOptionsChangedAsync(CloudEventStreamReadOptions readOptions)
    {
        this.readOptions = readOptions;
        if (this.readOptions.Partition != null && string.IsNullOrWhiteSpace(this.readOptions.Partition.Id)) return Task.CompletedTask;
        return this.ReadStreamAsync();
    }

}
