using CloudStreams.Core.Api.Client.Services;
using CloudStreams.Dashboard.StateManagement;
using CloudStreams.Gateway.Api.Client.Services;
using System.Reactive.Linq;

namespace CloudStreams.Dashboard.Components.ReadOptionsFormStateManagement;

/// <summary>
/// Represents a <see cref="CloudEventStreamReadOptions"/>'s form <see cref="ComponentStore{TState}"/>
/// </summary>
public class ReadOptionsFormStore
    : ComponentStore<ReadOptionsFormState>
{
    /// <summary>
    /// The service used to interact with the Cloud Streams Gateway API
    /// </summary>
    private ICloudStreamsApiClient cloudStreamsApi;

    /// <summary>
    /// Initializes a new <see cref="ReadOptionsFormStore"/>
    /// </summary>
    /// <param name="cloudStreamsApi">The service used to interact with the Cloud Streams Gateway API</param>
    public ReadOptionsFormStore(ICloudStreamsApiClient cloudStreamsApi)
        : base(new())
    {
        this.cloudStreamsApi = cloudStreamsApi;
    }

    /// <summary>
    /// Gets an <see cref="IObservable{T}"/> used to observe <see cref="ReadOptionsFormState.PartitionType"/> changes
    /// </summary>
    public IObservable<CloudEventPartitionType?> PartitionType => this.Select(state => state.PartitionType).DistinctUntilChanged();

    /// <summary>
    /// Gets an <see cref="IObservable{T}"/> used to observe <see cref="ReadOptionsFormState.PartitionId"/> changes
    /// </summary>
    public IObservable<string?> PartitionId => this.Select(state => state.PartitionId).DistinctUntilChanged();

    /// <summary>
    /// Gets an <see cref="IObservable{T}"/> used to observe <see cref="ReadOptionsFormState.Direction"/> changes
    /// </summary>
    public IObservable<StreamReadDirection> Direction => this.Select(state => state.Direction).DistinctUntilChanged();

    /// <summary>
    /// Gets an <see cref="IObservable{T}"/> used to observe <see cref="ReadOptionsFormState.Offset"/> changes
    /// </summary>
    public IObservable<long?> Offset => this.Select(state => state.Offset).DistinctUntilChanged();

    /// <summary>
    /// Gets an <see cref="IObservable{T}"/> used to observe <see cref="ReadOptionsFormState.Length"/> changes
    /// </summary>
    public IObservable<ulong?> Length => this.Select(state => state.Length).DistinctUntilChanged();

    /// <summary>
    /// Gets an <see cref="IObservable{T}"/> used to observe <see cref="ReadOptionsFormState.Partitions"/> changes
    /// </summary>
    public IObservable<List<string>?> Partitions => this.Select(state => state.Partitions).DistinctUntilChanged();

    /// <summary>
    /// Gets an <see cref="IObservable{T}"/> used to observe the resulting <see cref="CloudEventStreamReadOptions"/>
    /// </summary>
    public IObservable<CloudEventStreamReadOptions?> ReadOptions => this.Select(state =>
    {
        var options = new CloudEventStreamReadOptions() {
            Direction = state.Direction
        };
        if (state.PartitionType.HasValue)
        {
            var partition = new CloudEventPartitionReference()
            {
                Type = state.PartitionType.Value
            };
            if (!string.IsNullOrWhiteSpace(state.PartitionId))
            {
                partition.Id = state.PartitionId;
            }
            options.Partition = partition;
        }
        if (state.Offset.HasValue)
        {
            options.Offset = state.Offset.Value;
        }
        if (state.Length.HasValue)
        {
            options.Length = state.Length.Value;
        }
        return options;
    }).DistinctUntilChanged();

    /// <inheritdoc/>
    public override async Task InitializeAsync()
    {
        await base.InitializeAsync(); 
        this.PartitionType.SubscribeAsync(this.SetPartitionsAsync, cancellationToken: this.CancellationTokenSource.Token);
    }

    /// <summary>
    /// Sets the state's <see cref="ReadOptionsFormState.PartitionType"/>
    /// </summary>
    /// <param name="partitionType">The new <see cref="ReadOptionsFormState.PartitionType"/> value</param>
    public void SetPartitionType(CloudEventPartitionType? partitionType)
    {
        this.Reduce(state => state with
        {
            PartitionType = partitionType,
            PartitionId = null
        });
    }

    /// <summary>
    /// Sets the state's <see cref="ReadOptionsFormState.PartitionId"/>
    /// </summary>
    /// <param name="partitionId">The new <see cref="ReadOptionsFormState.PartitionId"/> value</param>
    public void SetPartitionId(string? partitionId)
    {
        this.Reduce(state => state with
        {
            PartitionId = partitionId
        });
    }

    /// <summary>
    /// Sets the state's <see cref="ReadOptionsFormState.Direction"/>
    /// </summary>
    /// <param name="direction">The new <see cref="ReadOptionsFormState.Direction"/> value</param>
    public void SetDirection(StreamReadDirection direction)
    {
        this.Reduce(state => state with
        {
            Direction = direction
        });
    }

    /// <summary>
    /// Sets the state's <see cref="ReadOptionsFormState.Offset"/>
    /// </summary>
    /// <param name="offset">The new <see cref="ReadOptionsFormState.Offset"/> value</param>
    public void SetOffset(long? offset)
    {
        this.Reduce(state => state with
        {
            Offset = offset
        });
    }

    /// <summary>
    /// Sets the state's <see cref="ReadOptionsFormState.Length"/>
    /// </summary>
    /// <param name="length">The new <see cref="ReadOptionsFormState.Length"/> value</param>
    public void SetLenght(ulong? length)
    {
        this.Reduce(state => state with
        {
            Length = length
        });
    }

    /// <summary>
    /// Gathers and sets the <see cref="ReadOptionsFormState.Partitions"/> based on the provided <see cref="CloudEventPartitionType"/>
    /// </summary>
    /// <param name="partitionType">The <see cref="CloudEventPartitionType"/> to gather the <see cref="ReadOptionsFormState.Partitions"/> with</param>
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
        var partitions = await (await this.cloudStreamsApi.CloudEvents.Partitions.ListPartitionsByTypeAsync(partitionType.Value, this.CancellationTokenSource.Token).ConfigureAwait(false)).ToListAsync().ConfigureAwait(false);
        this.Reduce(state => state with
        {
            Partitions = partitions!
        });
    }
}
