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

namespace CloudStreams.Dashboard.Components.AutoRefreshFormStateManagement;

/// <summary>
/// Represents the state of the form used to manipulate <see cref="StreamReadOptions"/>
/// </summary>
public record AutoRefreshFormState
{
    /// <summary>
    /// Gets/sets a boolean indicating if the auto refresh feature is enabled
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets/sets the interval (is seconds) at which the auto refresh feature should trigger
    /// </summary>
    public int Interval { get; set; } = 5;
}
