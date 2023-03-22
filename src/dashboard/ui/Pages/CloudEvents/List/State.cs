namespace CloudStreams.Dashboard.Pages.CloudEvents.List;

/// <summary>
/// Represents the Cloud Event list view state
/// </summary>
public record CloudEventListState
{

    /// <summary>
    /// Gets the current <see cref="StreamReadOptions"/>, used to configure the read query to perform
    /// </summary>
    public StreamReadOptions ReadOptions { get; set; } = new(StreamReadDirection.Backwards);

    /// <summary>
    /// Gets a <see cref="List{T}"/> that contains all cached <see cref="CloudEvent"/>s
    /// </summary>
    public List<CloudEvent>? CloudEvents { get; set; } = new();

    /// <summary>
    /// Gets/sets a boolean value that indicates whether data is currently being gathered
    /// </summary>
    public bool Loading { get; set; } = false;

}
