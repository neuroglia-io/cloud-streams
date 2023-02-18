using CloudStreams.Gateway.Api.Client.Services;
using CloudStreams.Dashboard.StateManagement;
using CloudStreams.Core.Data.Models;
using System.Reactive.Linq;

namespace CloudStreams.Dashboard.Pages.CloudEvents.List;

public class CloudEventListStore
    : ComponentStore<CloudEventListState>
{

    ICloudStreamsGatewayApiClient cloudStreamsGatewayApi;
    CloudEventStreamReadOptions readOptions;

    public CloudEventListStore(ICloudStreamsGatewayApiClient cloudStreamsGatewayApi) 
        : base(new())
    { 
        this.cloudStreamsGatewayApi = cloudStreamsGatewayApi;
        this.ReadOptions.Subscribe(async o => await this.OnReadOptionsChangedAsync(o).ConfigureAwait(false), token: this.CancellationTokenSource.Token);
        this.readOptions = new(StreamReadDirection.Backwards);
        this.Reduce(state => state with { ReadOptions = this.readOptions });
    }

    public IObservable<CloudEventStreamReadOptions> ReadOptions => this.Select(s => s.ReadOptions).DistinctUntilChanged();

    public IObservable<IAsyncEnumerable<CloudEvent?>?> CloudEvents => this.Select(s => s.CloudEvents).DistinctUntilChanged();

    public void SetStreamReadOptions(Func<CloudEventStreamReadOptions, CloudEventStreamReadOptions> optionsReducer)
    {
        this.Reduce(state => state with
        {
            ReadOptions = optionsReducer(state.ReadOptions)
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
