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

namespace CloudStreams.Core.Api.Services;

/// <summary>
/// Defines the fundamentals of a service used to manage the leases that let a single process at a time monitor the health of a resource
/// </summary>
/// <remarks>
/// Every gateway replica, and the standalone API, runs a health monitor for every gateway and broker: without leases, each service would be probed, and its status
/// patched, once per process (see #180)
/// </remarks>
public interface IHealthMonitorLeaseManager
{

    /// <summary>
    /// Gets the identifier of the lease owner, which is to say of the current process
    /// </summary>
    string OwnerId { get; }

    /// <summary>
    /// Acquires the specified lease, or renews it if the current process already holds it
    /// </summary>
    /// <param name="key">The key of the lease to acquire</param>
    /// <param name="namespace">The namespace of the monitored resource, if any, which a store of leases may keep the lease in</param>
    /// <param name="duration">How long the lease is held, unless it is renewed</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A boolean indicating whether or not the current process holds the lease</returns>
    Task<bool> TryAcquireAsync(string key, string? @namespace, TimeSpan duration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases the specified lease, if the current process holds it
    /// </summary>
    /// <param name="key">The key of the lease to release</param>
    /// <param name="namespace">The namespace of the monitored resource, if any</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    Task ReleaseAsync(string key, string? @namespace, CancellationToken cancellationToken = default);

}

/// <summary>
/// Represents the <see cref="IHealthMonitorLeaseManager"/> that stores leases in Redis
/// </summary>
/// <param name="redis">The service used to connect to Redis</param>
public class RedisHealthMonitorLeaseManager(IConnectionMultiplexer redis)
    : IHealthMonitorLeaseManager
{

    /// <summary>
    /// Gets the prefix of the keys leases are stored at
    /// </summary>
    public const string KeyPrefix = "cloud-streams:leases:health-monitors:";

    const string AcquireScript = """
        local owner = redis.call('GET', KEYS[1])
        if not owner then
            redis.call('SET', KEYS[1], ARGV[1], 'PX', ARGV[2])
            return 1
        end
        if owner == ARGV[1] then
            redis.call('PEXPIRE', KEYS[1], ARGV[2])
            return 1
        end
        return 0
        """;

    const string ReleaseScript = """
        if redis.call('GET', KEYS[1]) == ARGV[1] then
            return redis.call('DEL', KEYS[1])
        end
        return 0
        """;

    /// <inheritdoc/>
    public string OwnerId { get; } = $"{Environment.MachineName}/{Guid.NewGuid():N}";

    /// <inheritdoc/>
    public virtual async Task<bool> TryAcquireAsync(string key, string? @namespace, TimeSpan duration, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var result = await redis.GetDatabase().ScriptEvaluateAsync(AcquireScript, [KeyPrefix + key], [this.OwnerId, (long)Math.Max(1, duration.TotalMilliseconds)]).WaitAsync(cancellationToken).ConfigureAwait(false);
        return (long)result == 1;
    }

    /// <inheritdoc/>
    public virtual async Task ReleaseAsync(string key, string? @namespace, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        await redis.GetDatabase().ScriptEvaluateAsync(ReleaseScript, [KeyPrefix + key], [this.OwnerId]).WaitAsync(cancellationToken).ConfigureAwait(false);
    }

}
