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

namespace CloudStreams.Dashboard.Components.TimelineStateManagement;

/// <summary>
/// Represents a <see cref="Timeline"/>'s form <see cref="ComponentStore{TState}"/>
/// </summary>
/// <remarks>
/// Initializes a new <see cref="TimelineStore"/>
/// </remarks>
/// <param name="cloudStreamsApi">The service used to interact with a Cloud Streams gateway's API</param>
public class TimelineStore(ICloudStreamsCoreApiClient cloudStreamsApi)
    : ComponentStore<TimelineState>(new())
{

    /// <summary>
    /// Gets an <see cref="IObservable{T}"/> used to observe <see cref="TimelineState.StreamsReadOptions"/> changes
    /// </summary>
    public IObservable<IEnumerable<StreamReadOptions>> StreamsReadOptions => this.Select(state => state.StreamsReadOptions).DistinctUntilChanged();

    /// <summary>
    /// Gets an <see cref="IObservable{T}"/> used to observe <see cref="TimelineState.TimelineLanes"/> changes
    /// </summary>
    public IObservable<IEnumerable<TimelineLane>> TimelineLanes => this.Select(state => state.TimelineLanes)
        .DistinctUntilChanged()
        .Select(timelineLanes =>
            timelineLanes.Select(kvp => new TimelineLane() { Name = kvp.Key, Data = kvp.Value})
        );

    /// <summary>
    /// Gets an <see cref="IObservable{T}"/> used to observe <see cref="TimelineState.Loading"/> changes
    /// </summary>
    public IObservable<bool> Loading => this.Select(state => state.Loading).DistinctUntilChanged();

    /// <summary>
    /// Gets an <see cref="IObservable{T}"/> used to observe <see cref="TimelineState.KeepTimeRange"/> changes
    /// </summary>
    public IObservable<bool> KeepTimeRange => this.Select(state => state.KeepTimeRange).DistinctUntilChanged();

    /// <inheritdoc/>
    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        this.StreamsReadOptions.Throttle(TimeSpan.FromMilliseconds(100)).SubscribeAsync(async (_) => await this.GatherCloudEventsAsync(), cancellationToken: this.CancellationTokenSource.Token);
    }

    /// <summary>
    /// Sets a state's <see cref="TimelineState.StreamsReadOptions"/>
    /// </summary>
    /// <param name="index">The index of the <see cref="TimelineState.StreamsReadOptions"/> to set</param>
    /// <param name="readOptions">The new <see cref="TimelineState.StreamsReadOptions"/> value</param>
    public void SetStreamsReadOption(int index, StreamReadOptions readOptions)
    {
        var streamsReadOptions = new List<StreamReadOptions>(this.Get(state => state.StreamsReadOptions));
        if (streamsReadOptions.Count <= index)
        {
            streamsReadOptions.Add(readOptions);
        }
        else
        {
            streamsReadOptions.RemoveAt(index);
            streamsReadOptions.Insert(index, readOptions);
        }
        this.Reduce(state => state with
        {
            StreamsReadOptions = streamsReadOptions
        });
    }

    /// <summary>
    /// Adds a state's <see cref="TimelineState.StreamsReadOptions"/>
    /// </summary>
    public void AddStreamsReadOption()
    {
        var streamsReadOptions = new List<StreamReadOptions>(this.Get(state => state.StreamsReadOptions))
        {
            new(StreamReadDirection.Backwards)
        };
        this.Reduce(state => state with
        {
            StreamsReadOptions = streamsReadOptions
        });
    }

    /// <summary>
    /// Removes a state's <see cref="TimelineState.StreamsReadOptions"/>
    /// </summary>
    /// <param name="index">The index of the <see cref="TimelineState.StreamsReadOptions"/> to remove</param>
    public void RemoveStreamsReadOption(int index)
    {
        var streamsReadOptions = new List<StreamReadOptions>(this.Get(state => state.StreamsReadOptions));
        if (streamsReadOptions.Count <= index)
        {
            return;
        }
        streamsReadOptions.RemoveAt(index);
        this.Reduce(state => state with
        {
            StreamsReadOptions = streamsReadOptions
        });
    }

    /// <summary>
    /// Toggles the state's <see cref="TimelineState.KeepTimeRange"/>
    /// </summary>
    public void ToggleKeepTimeRange()
    {
        this.Reduce(state => state with { 
            KeepTimeRange = !state.KeepTimeRange
        });
    }

    /// <summary>
    /// Adds a <see cref="TimelineState.StreamsReadOptions"/> for the provided <see cref="PartitionReference"/> if none already exists
    /// </summary>
    /// <param name="partition">The <see cref="PartitionReference"/> to add <see cref="StreamReadOptions"/>'s for</param>
    public void TryAddPartitionReadOption(PartitionReference partition)
    {
        if (partition == null)
        {
            return;
        }
        var streamsReadOptions = this.Get(state => state.StreamsReadOptions);
        if (streamsReadOptions.Any(readOption => readOption.Partition?.Type == partition.Type && readOption.Partition?.Id == partition.Id))
        {
            return;
        }
        streamsReadOptions = new List<StreamReadOptions>(streamsReadOptions)
        {
            new(partition, StreamReadDirection.Backwards)
        };
        this.Reduce(state => state with
        {
            StreamsReadOptions = streamsReadOptions
        });
    }

    /// <summary>
    /// Gathers the <see cref="CloudEvent"/>s for the current <see cref="TimelineState.StreamsReadOptions"/>
    /// </summary>
    public async Task GatherCloudEventsAsync()
    {
        this.Reduce(state => state with
        {
            Loading = true
        });
        await Task.Delay(1);
        try
        {
            var streamsReadOptions = this.Get(state => state.StreamsReadOptions);
            if (streamsReadOptions == null || !streamsReadOptions.Any())
            {
                this.Reduce(state => state with
                {
                    Loading = false
                });
                return;
            }
            var lanes = new Dictionary<string, IEnumerable<CloudEvent>>();
            for(int optionsIndex = 0, c = streamsReadOptions.Count(); optionsIndex < c; optionsIndex++)
            {
                var options = streamsReadOptions.ElementAt(optionsIndex);
                if (options == null) continue;
                var readOptions = new StreamReadOptions
                {
                    Direction = options.Direction,
                    Offset = options.Offset,
                    Length = options.Length
                };
                string name;
                var data = new List<CloudEvent>();
                if (options.Partition?.Type == null || options.Partition?.Id == null)
                {
                    name = $"{optionsIndex+1}. All";
                }
                else
                {
                    readOptions.Partition = options.Partition;
                    name = $"{optionsIndex+1}. {options.Partition.Type} | {options.Partition.Id}";
                }
                if (readOptions.Length <= StreamReadOptions.MaxLength)
                {
                    var cloudEvents = await (await cloudStreamsApi.CloudEvents.Stream.ReadStreamAsync(readOptions, this.CancellationTokenSource.Token).ConfigureAwait(false)).ToListAsync().ConfigureAwait(false);
                    data.AddRange(cloudEvents!);
                }
                else
                {
                    bool fetchMore = true;
                    ulong length = StreamReadOptions.MaxLength;
                    long offset = options.Offset ?? (options.Direction == StreamReadDirection.Forwards ? 0 : -1);
                    do
                    {
                        readOptions.Offset = offset;
                        readOptions.Length = length;
                        var cloudEvents = await (await cloudStreamsApi.CloudEvents.Stream.ReadStreamAsync(readOptions, this.CancellationTokenSource.Token).ConfigureAwait(false)).ToListAsync().ConfigureAwait(false);
                        data.AddRange(cloudEvents!);
                        offset = (long)cloudEvents.Last()!.GetSequence()! + (options.Direction == StreamReadDirection.Forwards ? 1 : -1);
                        length = Math.Min(options.Length - (ulong)data.Count, StreamReadOptions.MaxLength);
                        fetchMore = cloudEvents.Count > 1 && length != 0;
                    }
                    while(fetchMore);
                }
                lanes.Add(name, data);
            }
            this.Reduce(state => state with { 
                TimelineLanes = lanes,
                Loading = false
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString()); // todo: improve logging
            this.Reduce(state => state with
            {
                Loading = false
            });
        }
    }
}
