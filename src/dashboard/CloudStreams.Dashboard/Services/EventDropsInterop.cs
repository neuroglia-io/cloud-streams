// Copyright © 2024-Present The Cloud Streams Authors
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

using CloudStreams.Dashboard.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace CloudStreams.Dashboard.Services;

/// <summary>
/// The service used to build a bridge with event drops
/// </summary>
/// <remarks>
/// Constructs a new <see cref="EventDropsInterop"/>
/// </remarks>
/// <param name="jsRuntime">The service used to interop with JS</param>
public class EventDropsInterop(IJSRuntime jsRuntime)
        : IAsyncDisposable
{

    readonly Lazy<Task<IJSObjectReference>> moduleTask = new(() => jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/event-drops-interop.js").AsTask());

    /// <summary>
    /// Renders a <see cref="Timeline"/> using event-drops
    /// </summary>
    /// <param name="domElement">The <see cref="ElementReference"/> to render the time to</param>
    /// <param name="dotnetReference">The <see cref="DotNetObjectReference{Task}"/> of the calling component</param>
    /// <param name="dataset">The event-drops dataset</param>
    /// <param name="start">The moment the timeline starts</param>
    /// <param name="end">The moment the timeline starts></param>
    /// <param name="keepTimeRange">A boolean indicating whether or not to keep the time range</param>
    public async ValueTask RenderTimelineAsync(ElementReference domElement, DotNetObjectReference<Timeline>? dotnetReference, IEnumerable<TimelineLane> dataset, DateTimeOffset start, DateTimeOffset end, bool keepTimeRange)
    {
        var module = await moduleTask.Value;
        await module.InvokeVoidAsync("renderTimeline", domElement, dotnetReference, dataset, start, end, keepTimeRange);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (moduleTask.IsValueCreated)
        {
            var module = await moduleTask.Value;
            await module.InvokeVoidAsync("dispose");
            await module.DisposeAsync();
        }
        GC.SuppressFinalize(this);
    }

}
