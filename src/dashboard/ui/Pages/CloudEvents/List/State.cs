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
    /// Gets an <see cref="IAsyncEnumerable{T}"/> used to asynchronously enumerate current <see cref="CloudEvent"/>s
    /// </summary>
    public IAsyncEnumerable<CloudEvent?>? CloudEvents { get; set; }

}
