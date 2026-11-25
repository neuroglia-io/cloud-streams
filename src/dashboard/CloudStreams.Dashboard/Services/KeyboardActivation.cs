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

using Microsoft.AspNetCore.Components.Web;

namespace CloudStreams.Dashboard.Services;

/// <summary>
/// Lets elements that are not buttons, such as table rows, be activated with the keyboard as they are with a click (see #198)
/// </summary>
public static class KeyboardActivation
{

    /// <summary>
    /// Activates the specified action when Enter or Space is pressed
    /// </summary>
    /// <param name="e">The keyboard event to handle</param>
    /// <param name="action">The action to activate</param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    public static Task OnKeyDown(KeyboardEventArgs e, Func<Task> action) => e.Key is "Enter" or " " ? action() : Task.CompletedTask;

}
