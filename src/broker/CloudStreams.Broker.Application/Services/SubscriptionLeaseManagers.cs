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

using CloudStreams.Broker.Application.Configuration;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace CloudStreams.Broker.Application.Services;

/// <summary>
/// Represents the Redis implementation of the <see cref="ISubscriptionLeaseManager"/> interface
/// </summary>
/// <remarks>
/// A lease is a Redis key holding the identifier of its owner, which expires unless renewed. Acquiring, renewing and releasing are atomic, so that two replicas can
/// never hold the same lease, and a replica can never renew or release a lease that has been taken over by another one (see #154)
/// </remarks>
public class RedisSubscriptionLeaseManager
    : ISubscriptionLeaseManager
{

    /// <summary>
    /// Gets the prefix of the Redis keys of subscription leases
    /// </summary>
    public const string KeyPrefix = "cloud-streams:leases:subscriptions:";

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

    /// <summary>
    /// Initializes a new <see cref="RedisSubscriptionLeaseManager"/>
    /// </summary>
    /// <param name="redis">The service used to connect to Redis</param>
    /// <param name="options">The service used to access the current <see cref="BrokerOptions"/></param>
    public RedisSubscriptionLeaseManager(IConnectionMultiplexer redis, IOptions<BrokerOptions> options)
    {
        this.Redis = redis;
        this.Options = options.Value.Leases;
        this.OwnerId = $"{options.Value.GetNamespace()}/{options.Value.GetName()}/{Environment.MachineName}/{Guid.NewGuid():N}";
    }

    /// <summary>
    /// Gets the service used to connect to Redis
    /// </summary>
    protected IConnectionMultiplexer Redis { get; }

    /// <summary>
    /// Gets the options used to configure leases
    /// </summary>
    protected SubscriptionLeaseOptions Options { get; }

    /// <inheritdoc/>
    public string OwnerId { get; }

    /// <inheritdoc/>
    public virtual async Task<bool> TryAcquireAsync(string subscription, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subscription);
        var result = await this.Redis.GetDatabase().ScriptEvaluateAsync(AcquireScript, [KeyPrefix + subscription], [this.OwnerId, (long)this.Options.Duration.TotalMilliseconds]).WaitAsync(cancellationToken).ConfigureAwait(false);
        return (long)result == 1;
    }

    /// <inheritdoc/>
    public virtual async Task ReleaseAsync(string subscription, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subscription);
        await this.Redis.GetDatabase().ScriptEvaluateAsync(ReleaseScript, [KeyPrefix + subscription], [this.OwnerId]).WaitAsync(cancellationToken).ConfigureAwait(false);
    }

}

/// <summary>
/// Represents an <see cref="ISubscriptionLeaseManager"/> that always grants leases, used when leases are disabled, e.g. for a single broker replica
/// </summary>
public class UnrestrictedSubscriptionLeaseManager
    : ISubscriptionLeaseManager
{

    /// <inheritdoc/>
    public string OwnerId { get; } = Environment.MachineName;

    /// <inheritdoc/>
    public Task<bool> TryAcquireAsync(string subscription, CancellationToken cancellationToken = default) => Task.FromResult(true);

    /// <inheritdoc/>
    public Task ReleaseAsync(string subscription, CancellationToken cancellationToken = default) => Task.CompletedTask;

}
