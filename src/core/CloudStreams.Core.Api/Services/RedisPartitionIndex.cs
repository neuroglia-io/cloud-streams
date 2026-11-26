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

using CloudStreams.Core.Application.Services;
using StackExchange.Redis;
using System.Text;

namespace CloudStreams.Core.Api.Services;

/// <summary>
/// Represents the Redis implementation of the <see cref="IPartitionIndex"/> interface
/// </summary>
/// <param name="redis">The service used to connect to Redis</param>
/// <remarks>
/// <para>
/// Each type of partition is a sorted set whose members all have the same score, so that Redis orders them lexicographically and serves ranges of them in logarithmic
/// time, which provides paging and prefix search without loading the whole index (see #185).
/// </para>
/// <para>
/// A member is the upper-cased id, a NUL separator and the id itself. Byte order then sorts ids case-insensitively first and exactly second, as defined by
/// <see cref="PartitionIdCollection.Comparer"/>, and a case-insensitive prefix is a range of upper-cased members
/// </para>
/// </remarks>
public class RedisPartitionIndex(IConnectionMultiplexer redis)
    : IPartitionIndex
{

    /// <summary>
    /// Gets the prefix of the Redis keys of the index
    /// </summary>
    public const string KeyPrefix = "cloud-streams:partitions:";

    const char Separator = '\0';

    /// <summary>
    /// Gets the Redis key of the index of the specified type of partitions
    /// </summary>
    /// <param name="namespace">The namespace the partitions belong to</param>
    /// <param name="partitionType">The type of partitions to get the key for</param>
    /// <returns>The Redis key of the index of the specified type of partitions</returns>
    /// <remarks>The partitions of the default namespace are kept in the keys of earlier versions, which indexed the partitions of the default namespace only (see #215)</remarks>
    public static RedisKey GetKey(string @namespace, CloudEventPartitionType partitionType) => @namespace == Neuroglia.Data.Infrastructure.ResourceOriented.Namespace.DefaultNamespaceName
        ? KeyPrefix + EnumHelper.Stringify(partitionType)
        : $"{KeyPrefix}namespaces:{@namespace}:{EnumHelper.Stringify(partitionType)}";

    static RedisKey GetInitializationKey(CloudEventPartitionType partitionType) => KeyPrefix + EnumHelper.Stringify(partitionType) + ":initialized";

    /// <summary>
    /// Gets the member that represents the specified id in the index
    /// </summary>
    /// <param name="id">The id to get the member for</param>
    /// <returns>The member that represents the specified id</returns>
    public static string ToMember(string id) => $"{id.ToUpperInvariant()}{Separator}{id}";

    static string FromMember(string member) => member[(member.IndexOf(Separator) + 1)..];

    /// <inheritdoc/>
    public virtual async Task AddAsync(string @namespace, IEnumerable<PartitionReference> partitions, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(partitions);
        var database = redis.GetDatabase();
        var tasks = partitions
            .Where(p => !string.IsNullOrEmpty(p.Id))
            .GroupBy(p => p.Type)
            .Select(g => database.SortedSetAddAsync(GetKey(@namespace, g.Key), [.. g.Select(p => p.Id).Distinct(StringComparer.Ordinal).Select(id => new SortedSetEntry(ToMember(id), 0))]));
        await Task.WhenAll(tasks).WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public virtual async Task<IReadOnlyList<string>> ListAsync(string @namespace, CloudEventPartitionType partitionType, string? prefix, string? after, int maxResults, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxResults, 1);
        RedisValue min = default;
        var exclude = Exclude.None;
        RedisValue max = default;
        if (!string.IsNullOrEmpty(prefix))
        {
            var upperPrefix = prefix.ToUpperInvariant();
            min = upperPrefix;
            // every member that starts with the prefix sorts before the prefix followed by the greatest byte
            max = (byte[])[.. Encoding.UTF8.GetBytes(upperPrefix), 0xFF];
        }
        if (after != null)
        {
            var afterMember = ToMember(after);
            if (min.IsNull || string.CompareOrdinal(afterMember, (string)min!) >= 0)
            {
                min = afterMember;
                exclude = Exclude.Start;
            }
        }
        var members = await redis.GetDatabase().SortedSetRangeByValueAsync(GetKey(@namespace, partitionType), min, max, exclude, Order.Ascending, 0, maxResults).WaitAsync(cancellationToken).ConfigureAwait(false);
        return [.. members.Select(m => FromMember(m!))];
    }

    /// <inheritdoc/>
    public virtual async Task<bool> IsInitializedAsync(CloudEventPartitionType partitionType, CancellationToken cancellationToken = default) =>
        await redis.GetDatabase().KeyExistsAsync(GetInitializationKey(partitionType)).WaitAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc/>
    public virtual Task MarkInitializedAsync(CloudEventPartitionType partitionType, CancellationToken cancellationToken = default) =>
        redis.GetDatabase().StringSetAsync(GetInitializationKey(partitionType), DateTimeOffset.UtcNow.ToString("O")).WaitAsync(cancellationToken);

}
