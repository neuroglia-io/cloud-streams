// Copyright © 2023-Present The Cloud Streams Authors
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
    /// Gets the total count of <see cref="CloudEvent"/>s for the stream/selected partition
    /// </summary>
    public ulong? TotalCount { get; set; } = null;

    /// <summary>
    /// Gets/sets a boolean value that indicates whether data is currently being gathered
    /// </summary>
    public bool Loading { get; set; } = false;

}
