using CloudStreams.Dashboard.StateManagement;
using System.Reactive.Linq;
using CloudStreams.Core.Api.Client.Services;

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
    /// <returns></returns>
    public async Task GatherCloudEvents()
    {
        this.Reduce(state => state with
        {
            Processing = true
        });
        try
        {
            var cloudEvents = new List<CloudEvent?>();
            var i = 0;
            //for(var i = 0; i<500; i++)
            //{
            var readOptions = new StreamReadOptions(StreamReadDirection.Backwards, null, 100);
            cloudEvents.AddRange(await (await this.cloudStreamsApi.CloudEvents.Stream.ReadStreamAsync(readOptions/*, this.CancellationTokenSource.Token*/).ConfigureAwait(false)).ToListAsync().ConfigureAwait(false));
            //}
            var start = cloudEvents.First()!.Time;
            var end = cloudEvents.Last()!.Time;
            var dataset = new List<Object>() { new
                {
                    name = "cloud events",
                    data = cloudEvents.Select(ce => new { date = ce.Time })
                }
            };
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
