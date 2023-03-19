namespace CloudStreams.Dashboard.Components.TimelineStateManagement;

/// <summary>
/// Represents the state of the <see cref="Timeline"/>'s component
/// </summary>
public record TimelineState
{
    /// <summary>
    /// Gets/sets the list of <see cref="StreamReadOptions"/> used to populate <see cref="TimelineLane"/>s
    /// </summary>
    public IEnumerable<StreamReadOptions> StreamsReadOptions { get; set; } = new List<StreamReadOptions>() { new StreamReadOptions(StreamReadDirection.Backwards) };
    /// <summary>
    /// Gets/sets the list of <see cref="TimelineLane"/> to build the <see cref="Timeline"/> with
    /// </summary>
    public IDictionary<string, IEnumerable<ITimelineData>> TimelineLanes { get; set; } = new Dictionary<string, IEnumerable<ITimelineData>>();
    /// <summary>
    /// Gets/sets the indicator of the data is being gathered
    /// </summary>
    public bool Processing { get; set; } = false;
}
