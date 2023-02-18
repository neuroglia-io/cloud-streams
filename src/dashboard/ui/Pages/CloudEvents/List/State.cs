using CloudStreams.Data.Models;

namespace CloudStreams.Dashboard.Pages.CloudEvents.List;

public record CloudEventListState
{

    public CloudEventStreamReadOptions ReadOptions { get; set; } = new();

    public IAsyncEnumerable<CloudEvent?>? CloudEvents { get; set; }

}
