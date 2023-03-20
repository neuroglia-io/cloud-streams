namespace CloudStreams.Dashboard.Components;

/// <summary>
/// Reprensent a <see cref="Timeline"/> data point
/// </summary>
public interface ITimelineData
{
    /// <summary>
    /// The date to display the element at in the <see cref="Timeline"/>
    /// </summary>
    DateTimeOffset Date { get; }
}
