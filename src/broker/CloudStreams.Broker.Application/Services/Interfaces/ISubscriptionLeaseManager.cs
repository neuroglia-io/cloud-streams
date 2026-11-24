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

namespace CloudStreams.Broker.Application.Services;

/// <summary>
/// Defines the fundamentals of a service used to manage the leases brokers hold on the subscriptions they handle, so that a subscription is handled by a single broker
/// replica at a time
/// </summary>
public interface ISubscriptionLeaseManager
{

    /// <summary>
    /// Gets the identifier of the lease owner, which is to say of the current broker replica
    /// </summary>
    string OwnerId { get; }

    /// <summary>
    /// Acquires the lease on the specified subscription, or renews it if the current replica already holds it
    /// </summary>
    /// <param name="subscription">The key of the subscription to acquire the lease on</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A boolean indicating whether or not the current replica holds the lease</returns>
    Task<bool> TryAcquireAsync(string subscription, CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases the lease on the specified subscription, if the current replica holds it
    /// </summary>
    /// <param name="subscription">The key of the subscription to release the lease on</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    Task ReleaseAsync(string subscription, CancellationToken cancellationToken = default);

}
