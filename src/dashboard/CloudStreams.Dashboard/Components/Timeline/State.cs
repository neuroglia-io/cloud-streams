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

namespace CloudStreams.Dashboard.Components.TimelineStateManagement;

/// <summary>
/// Represents the state of the <see cref="Timeline"/>'s component
/// </summary>
public record TimelineState
{
    /// <summary>
    /// Gets/sets the list of <see cref="StreamReadOptions"/> used to populate <see cref="TimelineLane"/>s
    /// </summary>
    public IEnumerable<StreamReadOptions> StreamsReadOptions { get; set; } = [new StreamReadOptions(StreamReadDirection.Backwards)];
    /// <summary>
    /// Gets/sets the list of <see cref="TimelineLane"/> to build the <see cref="Timeline"/> with
    /// </summary>
    public IDictionary<string, IEnumerable<CloudEvent>> TimelineLanes { get; set; } = new Dictionary<string, IEnumerable<CloudEvent>>();
    /// <summary>
    /// Gets/sets a boolean value that indicates whether data is currently being gathered
    /// </summary>
    public bool Loading { get; set; } = false;
}
