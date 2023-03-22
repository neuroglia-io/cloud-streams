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

namespace CloudStreams.Core;

/// <summary>
/// Exposes methods to help handling <see cref="Uri"/>s
/// </summary>
public static class UriHelper
{

    /// <summary>
    /// Combines the specified components<para></para>
    /// Leading and trailing slash characters will be removed from all components
    /// </summary>
    /// <param name="components">The components to combine</param>
    /// <returns>A new <see cref="Uri"/></returns>
    public static Uri Combine(params string[] components) => new(string.Join('/', components.Select(c => c.Trim('/'))));

}
