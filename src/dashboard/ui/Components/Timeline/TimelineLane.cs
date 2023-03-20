namespace CloudStreams.Dashboard.Components;

/// <summary>
/// Represent a lane in a <see cref="Timeline"/>
/// </summary>
public class TimelineLane
{
    /// <summary>
    /// The name of the lane
    /// </summary>
    public string Name { get; set; }
    /// <summary>
    /// The set of data to render
    /// </summary>
    public IEnumerable<CloudEvent> Data { get; set; }
}
