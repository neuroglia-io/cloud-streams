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

using StackExchange.Redis;

namespace CloudStreams.Gateway.Application.Services;

/// <summary>
/// Enumerates the outcomes of a deduplication reservation
/// </summary>
public enum DeduplicationOutcome
{
    /// <summary>
    /// Indicates that the cloud event has not been seen within the deduplication window: it has been reserved, and must be ingested, then confirmed or released
    /// </summary>
    Reserved,
    /// <summary>
    /// Indicates that the cloud event has already been stored within the deduplication window
    /// </summary>
    Duplicate,
    /// <summary>
    /// Indicates that the cloud event is being ingested concurrently, and may yet fail to be stored
    /// </summary>
    InFlight
}

/// <summary>
/// Defines the fundamentals of a service used to deduplicate the cloud events ingested by a gateway, by their <c>source</c> and <c>id</c> (see #209)
/// </summary>
/// <remarks>
/// A cloud event is reserved before it is stored, and confirmed once it has been, so that a cloud event that failed to be stored is never mistaken for a duplicate:
/// its reservation is released, or expires shortly if the gateway dies in the meantime
/// </remarks>
public interface ICloudEventDeduplicator
{

    /// <summary>
    /// Reserves the specified cloud event, unless it has already been stored, or is being ingested, within the deduplication window
    /// </summary>
    /// <param name="scope">The qualified name of the gateway that ingests the cloud event: gateways sharing a database deduplicate independently</param>
    /// <param name="e">The cloud event to reserve</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The outcome of the reservation</returns>
    Task<DeduplicationOutcome> TryReserveAsync(string scope, CloudEvent e, CancellationToken cancellationToken = default);

    /// <summary>
    /// Confirms that the specified reserved cloud event has been stored: its duplicates are ignored until the deduplication window elapses
    /// </summary>
    /// <param name="scope">The qualified name of the gateway that ingests the cloud event</param>
    /// <param name="e">The cloud event that has been stored</param>
    /// <param name="window">The deduplication window</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    Task ConfirmAsync(string scope, CloudEvent e, TimeSpan window, CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases the reservation of the specified cloud event, which failed to be stored, so that it can be published again
    /// </summary>
    /// <param name="scope">The qualified name of the gateway that ingests the cloud event</param>
    /// <param name="e">The cloud event to release</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    Task ReleaseAsync(string scope, CloudEvent e, CancellationToken cancellationToken = default);

}

/// <summary>
/// Represents the Redis implementation of the <see cref="ICloudEventDeduplicator"/> interface, shared by all replicas of a gateway
/// </summary>
/// <param name="redis">The service used to connect to Redis</param>
public class RedisCloudEventDeduplicator(IConnectionMultiplexer redis)
    : ICloudEventDeduplicator
{

    /// <summary>
    /// Gets the prefix of the keys that remember ingested cloud events
    /// </summary>
    public const string KeyPrefix = "cloud-streams:dedup:";

    /// <summary>
    /// Gets how long a reservation lasts if it is neither confirmed nor released, e.g. because the gateway died while ingesting the cloud event
    /// </summary>
    public static readonly TimeSpan ReservationDuration = TimeSpan.FromSeconds(30);

    const string Pending = "pending";
    const string Stored = "stored";

    /// <summary>
    /// Gets the service used to connect to Redis
    /// </summary>
    protected IConnectionMultiplexer Redis { get; } = redis;

    /// <summary>
    /// Gets the key that remembers the specified cloud event
    /// </summary>
    /// <param name="scope">The qualified name of the gateway that ingests the cloud event</param>
    /// <param name="e">The cloud event to get the key of</param>
    /// <returns>The key that remembers the specified cloud event</returns>
    /// <remarks>The source is length-prefixed, and the scope, a qualified resource name, cannot contain a colon, so that no source and id can be confused with another pair</remarks>
    protected static RedisKey GetKey(string scope, CloudEvent e) => $"{KeyPrefix}{scope}:{e.Source.OriginalString.Length}:{e.Source.OriginalString}:{e.Id}";

    /// <inheritdoc/>
    public virtual async Task<DeduplicationOutcome> TryReserveAsync(string scope, CloudEvent e, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(e);
        var database = this.Redis.GetDatabase();
        var key = GetKey(scope, e);
        if (await database.StringSetAsync(key, Pending, ReservationDuration, When.NotExists).WaitAsync(cancellationToken).ConfigureAwait(false)) return DeduplicationOutcome.Reserved;
        var state = await database.StringGetAsync(key).WaitAsync(cancellationToken).ConfigureAwait(false);
        if (state == Stored) return DeduplicationOutcome.Duplicate;
        if (state == Pending) return DeduplicationOutcome.InFlight;
        // the key expired in the meantime: whatever it remembered no longer applies
        return await database.StringSetAsync(key, Pending, ReservationDuration, When.NotExists).WaitAsync(cancellationToken).ConfigureAwait(false) ? DeduplicationOutcome.Reserved : DeduplicationOutcome.InFlight;
    }

    /// <inheritdoc/>
    public virtual Task ConfirmAsync(string scope, CloudEvent e, TimeSpan window, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(e);
        return this.Redis.GetDatabase().StringSetAsync(GetKey(scope, e), Stored, window).WaitAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public virtual Task ReleaseAsync(string scope, CloudEvent e, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(e);
        return this.Redis.GetDatabase().KeyDeleteAsync(GetKey(scope, e)).WaitAsync(cancellationToken);
    }

}
