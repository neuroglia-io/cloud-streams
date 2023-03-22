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

namespace CloudStreams.Core.Infrastructure.Services;

/// <summary>
/// Defines the fundamentals of a service used to monitor the state of a specific resource
/// </summary>
/// <typeparam name="TResource">The type of resource to monitor the state of</typeparam>
public interface IResourceMonitor<TResource>
    : IObservable<TResource>, IDisposable
{

    /// <summary>
    /// Gets the current state of the resource
    /// </summary>
    TResource Resource { get; }

    /// <summary>
    /// Gets a boolean indicating whether or not the <see cref="IResourceMonitor{TResource}"/> is monitoring the resource's state
    /// </summary>
    bool Running { get; }

    /// <summary>
    /// Starts monitoring the resource
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new awaitable <see cref="ValueTask"/></returns>
    ValueTask StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops monitoring the resource
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new awaitable <see cref="ValueTask"/></returns>
    ValueTask StopAsync(CancellationToken cancellationToken = default);

}