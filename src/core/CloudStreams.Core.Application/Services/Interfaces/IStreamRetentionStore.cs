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

namespace CloudStreams.Core.Application.Services;

/// <summary>
/// Defines the fundamentals of a service used to get and set the retention policy of the cloud event stream (see #206)
/// </summary>
public interface IStreamRetentionStore
{

    /// <summary>
    /// Gets the retention policy of the cloud event stream
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The retention policy of the cloud event stream, or null if it retains every event</returns>
    Task<StreamRetention?> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the retention policy of the cloud event stream
    /// </summary>
    /// <param name="retention">The retention policy to set. An unlimited one removes the current policy</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    Task SetAsync(StreamRetention retention, CancellationToken cancellationToken = default);

}
