using CloudStreams.Dashboard.StateManagement;
using System.Reactive.Linq;
using CloudStreams.Core.Api.Client.Services;
using Microsoft.Extensions.Options;
using System.Collections.Generic;

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
    public IObservable<IEnumerable<TimelineLane>> TimelineLanes => this.Select(state => 
        state.TimelineLanes.Select(kvp => new TimelineLane() { Name = kvp.Key, Data = kvp.Value})
    ).DistinctUntilChanged();

    /// <summary>
    /// Gets an <see cref="IObservable{T}"/> used to observe <see cref="TimelineState.Processing"/> changes
    /// </summary>
    public IObservable<bool> Processing => this.Select(state => state.Processing).DistinctUntilChanged();

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
            Processing = true
        });
        try
        {
            var streamsReadOptions = this.Get(state => state.StreamsReadOptions);
            if (streamsReadOptions == null || !streamsReadOptions.Any())
            {
                return;
            }
            var lanes = new Dictionary<string, IEnumerable<ITimelineData>>();
            for(int streamIndex = 0, c = streamsReadOptions.Count(); streamIndex < c; streamIndex++)
            {
                var options = streamsReadOptions.ElementAt(streamIndex);
                string name;
                List<ITimelineData> data = new List<ITimelineData>();
                if (options?.Partition?.Type == null || options?.Partition?.Id == null)
                {
                    name = $"{streamIndex+1}. All";
                }
                else
                {
                    name = $"{streamIndex+1}. {options.Partition.Type.ToString()} | {options.Partition.Id}";
                }
                for(ulong offset = 0; offset < options!.Length; offset += 100)
                {
                    var readOptions = new StreamReadOptions(StreamReadDirection.Backwards, (long)offset, 100);
                    var cloudEvents = await (await this.cloudStreamsApi.CloudEvents.Stream.ReadStreamAsync(readOptions, this.CancellationTokenSource.Token).ConfigureAwait(false)).ToListAsync().ConfigureAwait(false);
                    data.AddRange(cloudEvents.Select(e => new TimelineCloudEvent(e!)));
                }
                lanes.Add(name, data);
            }
            this.Reduce(state => state with { 
                TimelineLanes = lanes
            });
        }
        catch (Exception ex)
        {

        }
        this.Reduce(state => state with
        {
            Processing = false
        });
    }
}
