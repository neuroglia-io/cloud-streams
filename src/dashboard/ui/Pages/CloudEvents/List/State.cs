using CloudStreams.Core.Data.Models;

namespace CloudStreams.Dashboard.Pages.CloudEvents.List;

/// <summary>
/// Represents the Cloud Event list view state
/// </summary>
public record CloudEventListState
{

    /// <summary>
    /// Gets the current <see cref="CloudEventStreamReadOptions"/>, used to configure the read query to perform
    /// </summary>
    public CloudEventStreamReadOptions ReadOptions { get; set; } = new();

    /// <summary>
    /// Gets a <see cref="List{T}"/> that contains all cached <see cref="CloudEvent"/>s
    /// </summary>
    public List<CloudEvent>? CloudEvents { get; set; }

}
