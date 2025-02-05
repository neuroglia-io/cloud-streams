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


namespace CloudStreams.Dashboard;

/// <summary>
/// Represents the method that will handle an event that has no event data and returns a <see cref="Task"/>
/// </summary>
/// <param name="sender">The source of the event</param>
/// <returns>The <see cref="Task"/> handling the event</returns>
public delegate Task TaskEventHandler(object? sender);

/// <summary>
/// Represents an object used to manage the refreshing of a component
/// </summary>
public interface Refresher
{
    /// <summary>
    /// The event triggered a refresh is triggered
    /// </summary>
    public event TaskEventHandler? Refreshed;
}
