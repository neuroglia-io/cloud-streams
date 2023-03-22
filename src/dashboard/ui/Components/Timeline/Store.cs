// Copyright © 2023-Present The Cloud Streams Authors
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

using CloudStreams.Dashboard.StateManagement;
using System.Reactive.Linq;
using CloudStreams.Core.Api.Client.Services;
using Microsoft.Extensions.Options;
using System.Collections.Generic;
using CloudStreams.Core;

namespace CloudStreams.Dashboard.Components.TimelineStateManagement;

/// <summary>
/// Represents a <see cref="Timeline"/>'s form <see cref="ComponentStore{TState}"/>
/// </summary>
public class TimelineStore
    : ComponentStore<TimelineState>
{
    /// <summary>
    /// The service used to interact with a Cloud Streams gateway's API
    /// </summary>
    ICloudStreamsApiClient cloudStreamsApi;

    /// <summary>
    /// Initializes a new <see cref="TimelineStore"/>
    /// </summary>
    /// <param name="cloudStreamsApi">The service used to interact with a Cloud Streams gateway's API</param>
    public TimelineStore(ICloudStreamsApiClient cloudStreamsApi)
        : base(new())
    {
        this.cloudStreamsApi = cloudStreamsApi;
    }

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
        var streamsReadOptions = new List<StreamReadOptions>(this.Get(state => state.StreamsReadOptions));
        streamsReadOptions.Add(new StreamReadOptions(StreamReadDirection.Backwards));
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
                string name;
                List<CloudEvent> data = new List<CloudEvent>();
                if (options?.Partition?.Type == null || options?.Partition?.Id == null)
                {
                    name = $"{optionsIndex+1}. All";
                }
                else
                {
                    name = $"{optionsIndex+1}. {options.Partition.Type.ToString()} | {options.Partition.Id}";
                }
                if (options!.Length <= StreamReadOptions.MaxLength)
                {
                    var cloudEvents = await (await this.cloudStreamsApi.CloudEvents.Stream.ReadStreamAsync(options, this.CancellationTokenSource.Token).ConfigureAwait(false)).ToListAsync().ConfigureAwait(false);
                    data.AddRange(cloudEvents!);
                }
                else
                {
                    bool fetchMore = true;
                    long offset = options.Offset ?? (options.Direction == StreamReadDirection.Forwards ? 0 : -1);
                    do
                    {
                        var readOptions = new StreamReadOptions();
                        readOptions.Direction = options.Direction;
                        readOptions.Partition = options.Partition;
                        readOptions.Offset = offset;
                        readOptions.Length = StreamReadOptions.MaxLength;
                        var cloudEvents = await (await this.cloudStreamsApi.CloudEvents.Stream.ReadStreamAsync(readOptions, this.CancellationTokenSource.Token).ConfigureAwait(false)).ToListAsync().ConfigureAwait(false);
                        data.AddRange(cloudEvents!);
                        offset = (long)cloudEvents.Last()!.GetSequence()!;
                        fetchMore = cloudEvents.Count() > 1 && (ulong)data.Count < options!.Length;
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
