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

using CloudStreams.Core.Resources;
using StackExchange.Redis;

namespace CloudStreams.Gateway.Application.Services;

/// <summary>
/// Describes the outcome of the evaluation of a cloud event against the rate limits of a gateway
/// </summary>
/// <param name="Allowed">A boolean indicating whether or not the cloud event is within every limit</param>
/// <param name="RetryAfter">The amount of time after which the cloud event may be published again, if it is not allowed</param>
public record RateLimitDecision(bool Allowed, TimeSpan? RetryAfter = null)
{

    /// <summary>
    /// Gets a <see cref="RateLimitDecision"/> that allows a cloud event
    /// </summary>
    public static readonly RateLimitDecision Allow = new(true);

}

/// <summary>
/// Defines the fundamentals of a service used to limit the rate at which cloud events are published to a gateway (see #210)
/// </summary>
public interface ICloudEventRateLimiter
{

    /// <summary>
    /// Counts the specified cloud event against the specified limits
    /// </summary>
    /// <param name="scope">The qualified name of the gateway that ingests the cloud event: gateways sharing a database count independently</param>
    /// <param name="e">The cloud event to count</param>
    /// <param name="publisher">The name of the publisher of the cloud event, if it has authenticated</param>
    /// <param name="limits">The limits to evaluate the cloud event against</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="RateLimitDecision"/></returns>
    Task<RateLimitDecision> EvaluateAsync(string scope, CloudEvent e, string? publisher, IReadOnlyList<CloudEventRateLimit> limits, CancellationToken cancellationToken = default);

}

/// <summary>
/// Represents the Redis implementation of the <see cref="ICloudEventRateLimiter"/> interface, whose counters are shared by all replicas of a gateway
/// </summary>
/// <param name="redis">The service used to connect to Redis</param>
/// <remarks>
/// Cloud events are counted in fixed windows of each limit's period, aligned on the Unix epoch. A cloud event refused by a limit is still counted against the limits
/// evaluated before it, which only makes the limits stricter
/// </remarks>
public class RedisCloudEventRateLimiter(IConnectionMultiplexer redis)
    : ICloudEventRateLimiter
{

    /// <summary>
    /// Gets the prefix of the keys of the counters
    /// </summary>
    public const string KeyPrefix = "cloud-streams:rate-limits:";

    /// <summary>
    /// Gets the name of the partition shared by anonymous publishers
    /// </summary>
    public const string AnonymousPublisher = "anonymous";

    /// <summary>
    /// Increments the counter, and sets its expiry when it is created, so that it is removed once its window has elapsed
    /// </summary>
    const string IncrementScript = @"
local count = redis.call('INCR', KEYS[1])
if count == 1 then redis.call('PEXPIRE', KEYS[1], ARGV[1]) end
return count";

    /// <summary>
    /// Gets the service used to connect to Redis
    /// </summary>
    protected IConnectionMultiplexer Redis { get; } = redis;

    /// <inheritdoc/>
    public virtual async Task<RateLimitDecision> EvaluateAsync(string scope, CloudEvent e, string? publisher, IReadOnlyList<CloudEventRateLimit> limits, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(e);
        ArgumentNullException.ThrowIfNull(limits);
        if (limits.Count == 0) return RateLimitDecision.Allow;
        var database = this.Redis.GetDatabase();
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        for (var index = 0; index < limits.Count; index++)
        {
            var limit = limits[index];
            var period = (long)limit.Period.ToTimeSpan().TotalMilliseconds;
            if (period <= 0 || limit.Limit <= 0) continue;
            var partition = limit.Partition == CloudEventRateLimitPartitioning.Source ? e.Source.OriginalString : string.IsNullOrWhiteSpace(publisher) ? AnonymousPublisher : publisher;
            var window = now / period;
            // the key identifies the limit by its definition rather than its position, so that editing the limits does not mix up their counters
            RedisKey key = $"{KeyPrefix}{scope}:{limit.Partition}:{limit.Limit}:{period}:{window}:{partition}";
            var count = (long)await database.ScriptEvaluateAsync(IncrementScript, [key], [period]).WaitAsync(cancellationToken).ConfigureAwait(false);
            if (count > limit.Limit) return new(false, TimeSpan.FromMilliseconds((window + 1) * period - now));
        }
        return RateLimitDecision.Allow;
    }

}
