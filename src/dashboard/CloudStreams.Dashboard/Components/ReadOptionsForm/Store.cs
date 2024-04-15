// Copyright © 2024-Present The Cloud Streams Authors
//
// Licensed under the Apache License, Version 2.0 (the "License"),
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using CloudStreams.Core.Api.Client.Services;

namespace CloudStreams.Dashboard.Components.ReadOptionsFormStateManagement;

/// <summary>
/// Represents a <see cref="StreamReadOptions"/>'s form <see cref="ComponentStore{TState}"/>
/// </summary>
/// <remarks>
/// Initializes a new <see cref="ReadOptionsFormStore"/>
/// </remarks>
/// <param name="cloudStreamsApi">The service used to interact with the Cloud Streams Gateway API</param>
public class ReadOptionsFormStore(ICloudStreamsCoreApiClient cloudStreamsApi)
    : ComponentStore<ReadOptionsFormState>(new())
{

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
    protected IObservable<ulong?> FactualLength => this.Select(state => state.Length).DistinctUntilChanged();

    /// <summary>
    /// Gets an <see cref="IObservable{T}"/> used to observe <see cref="ReadOptionsFormState.StreamLength"/> changes
    /// </summary>
    protected IObservable<ulong?> StreamLength => this.Select(state => state.StreamLength).DistinctUntilChanged();

    /// <summary>
    /// Gets an <see cref="IObservable{T}"/> used to observe <see cref="ReadOptionsFormState.StreamLength"/> changes
    /// </summary>
    public IObservable<ulong?> MaxLength => Observable.CombineLatest(
        this.Direction,
        this.Offset,
        this.StreamLength,
        (direction, offset, length) =>
        {
            if (length == null) return null;
            if (offset == null || offset == 0 || offset == -1) return length;
            if (direction == StreamReadDirection.Backwards) return (ulong?)offset + 1;
            return length - (ulong?)offset;
        }
    );

    /// <summary>
    /// Gets an <see cref="IObservable{T}"/> used to observe the computed length 
    /// </summary>
    public IObservable<ulong?> Length => Observable.CombineLatest(
        this.FactualLength,
        this.MaxLength,
        (length, maxLength) =>
        {
            if (!length.HasValue) return null;
            if (!maxLength.HasValue) return length;
            return Math.Min(length.Value, maxLength.Value);
        } 
    ).DistinctUntilChanged();


    /// <summary>
    /// Gets an <see cref="IObservable{T}"/> used to observe <see cref="ReadOptionsFormState.Partitions"/> changes
    /// </summary>
    public IObservable<List<string>?> Partitions => this.Select(state => state.Partitions).DistinctUntilChanged();

    /// <summary>
    /// Gets an <see cref="IObservable{T}"/> used to observe the resulting <see cref="StreamReadOptions"/>
    /// </summary>
    public IObservable<StreamReadOptions?> ReadOptions => Observable.CombineLatest(
        this.Direction,
        this.PartitionType,
        this.PartitionId,
        this.Offset,
        this.Length,
        (direction, partitionType, partitionId, offset, length) =>
        {
            var options = new StreamReadOptions()
            {
                Direction = direction
            };
            if (partitionType.HasValue)
            {
                var partition = new PartitionReference()
                {
                    Type = partitionType.Value
                };
                if (!string.IsNullOrWhiteSpace(partitionId)) partition.Id = partitionId;
                options.Partition = partition;
            }
            if (offset.HasValue) options.Offset = offset.Value;
            if (length.HasValue) options.Length = length.Value;
            return options;
        }
    );

    /// <summary>
    /// Gets an <see cref="IObservable{T}"/> used to observe the resulting partition
    /// </summary>
    protected IObservable<(CloudEventPartitionType?, string?)> Partition => Observable.CombineLatest(
        this.PartitionType,
        this.PartitionId,
        (type, id) =>
        {
            return (type, id);
        }
    ).Throttle(TimeSpan.FromMilliseconds(100)).DistinctUntilChanged();

    /// <inheritdoc/>
    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        this.PartitionType.SubscribeAsync(this.UpdatePartitionsAsync, cancellationToken: this.CancellationTokenSource.Token);
        this.Partition.SubscribeAsync(this.UpdateMetadataAsync, cancellationToken: this.CancellationTokenSource.Token);
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
    public void SetLength(ulong? length)
    {
        this.Reduce(state => state with
        {
            Length = length
        });
    }

    /// <summary>
    /// Resets the state with new <see cref="StreamReadOptions"/>
    /// </summary>
    /// <param name="readOptions">The new <see cref="StreamReadOptions"/> to reset the state with</param>
    public void SetReadOptions(StreamReadOptions readOptions)
    {
        var state = this.Get() with { };
        bool changed = false;
        if (state.Direction != readOptions.Direction)
        {
            state.Direction = readOptions.Direction;
            changed = true;
        }
        if (state.Offset != readOptions.Offset)
        {
            state.Offset = readOptions.Offset;
            changed = true;
        }
        //if (state.Length != readOptions.Length)
        //{
        //    state.Length = readOptions.Length;
        //    changed = true;
        //}
        if (state.PartitionType != readOptions.Partition?.Type)
        {
            state.PartitionType = readOptions.Partition?.Type;
            changed = true;
        }
        if (state.PartitionId != readOptions.Partition?.Id)
        {
            state.PartitionId = readOptions.Partition?.Id;
            changed = true;
        }
        if (changed)
        {
            state.Partitions = [];
            this.Reduce(_ => state);
        }
    }

    /// <summary>
    /// Gathers and sets the <see cref="ReadOptionsFormState.Partitions"/> based on the provided <see cref="CloudEventPartitionType"/>
    /// </summary>
    /// <param name="partitionType">The <see cref="CloudEventPartitionType"/> to gather the <see cref="ReadOptionsFormState.Partitions"/> with</param>
    /// <returns></returns>
    protected async Task UpdatePartitionsAsync(CloudEventPartitionType? partitionType)
    {
        if (!partitionType.HasValue)
        {
            this.Reduce(state => state with
            {
                Partitions = null
            });
            return;
        }
        var partitions = await (await cloudStreamsApi.CloudEvents.Partitions.ListPartitionsByTypeAsync(partitionType.Value, this.CancellationTokenSource.Token).ConfigureAwait(false)).ToListAsync().ConfigureAwait(false);
        this.Reduce(state => state with
        {
            Partitions = partitions!
        });
    }

    /// <summary>
    /// Updates <see cref="ReadOptionsFormState.StreamLength"/> based on the stream/partition's metadata
    /// </summary>
    /// <param name="partition">A (<see cref="CloudEventPartitionType"/>, string) tuple to gather the partition for, if any </param>
    protected async Task UpdateMetadataAsync((CloudEventPartitionType?, string?) partition)
    {
        this.Reduce(state => state with
        {
            StreamLength = null
        });
        try { 
            (CloudEventPartitionType? type, string? id) = partition;
            if (!type.HasValue || string.IsNullOrWhiteSpace(id))
            {
                StreamMetadata metadata = await cloudStreamsApi.CloudEvents.Stream.GetStreamMetadataAsync(this.CancellationTokenSource.Token).ConfigureAwait(false);
                this.Reduce(state => state with
                {
                    StreamLength = metadata.Length
                });
            }
            else
            {
                PartitionMetadata? metadata = await cloudStreamsApi.CloudEvents.Partitions.GetPartitionMetadataAsync(type!.Value, id!, this.CancellationTokenSource.Token).ConfigureAwait(false);
                if (metadata != null)
                {
                    this.Reduce(state => state with
                    {
                        StreamLength = metadata.Length
                    });
                }
            }
        }
        catch(Exception ex)
        {
            Console.WriteLine(ex.ToString()); // todo: improve logging
        }
    }
}
