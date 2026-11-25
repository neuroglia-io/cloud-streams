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
using Neuroglia.Data.Infrastructure.EventSourcing;

namespace CloudStreams.Core.Application.Queries.Streams;

/// <summary>
/// Represents the query used to describe the recent activity of the cloud event stream (see #225)
/// </summary>
/// <param name="period">The period, ending now, to describe the activity of</param>
/// <param name="buckets">The amount of consecutive slices to split the period into</param>
/// <param name="sampleSize">The amount of most recent cloud events to compute the most active sources and types from</param>
public class GetEventStreamActivityQuery(TimeSpan period, int buckets, int sampleSize)
    : Query<StreamActivity>
{

    /// <summary>
    /// Gets the default period
    /// </summary>
    public static readonly TimeSpan DefaultPeriod = TimeSpan.FromHours(24);

    /// <summary>
    /// Gets the longest period that may be described
    /// </summary>
    public static readonly TimeSpan MaxPeriod = TimeSpan.FromDays(31);

    /// <summary>
    /// Gets the default amount of buckets
    /// </summary>
    public const int DefaultBuckets = 24;

    /// <summary>
    /// Gets the default sample size
    /// </summary>
    public const int DefaultSampleSize = 500;

    /// <summary>
    /// Gets the period, ending now, to describe the activity of
    /// </summary>
    public TimeSpan Period { get; } = period;

    /// <summary>
    /// Gets the amount of consecutive slices to split the period into
    /// </summary>
    public int Buckets { get; } = buckets;

    /// <summary>
    /// Gets the amount of most recent cloud events to compute the most active sources and types from
    /// </summary>
    public int SampleSize { get; } = sampleSize;

}

/// <summary>
/// Represents the service used to handle <see cref="GetEventStreamActivityQuery"/> instances
/// </summary>
/// <param name="eventStore">The service used to read the cloud event stream</param>
/// <remarks>
/// The amount of cloud events recorded during a slice is the difference between the offsets of the first cloud events recorded at or after its bounds, each found
/// by a binary search on the timestamps of the stream, narrowed by the bound found before it. Describing a period costs a few dozen reads per bucket, whatever the
/// amount of cloud events recorded
/// </remarks>
public class GetEventStreamActivityQueryHandler(ICloudEventStore eventStore)
    : IQueryHandler<GetEventStreamActivityQuery, StreamActivity>
{

    /// <summary>
    /// Gets the amount of most active sources and types returned
    /// </summary>
    public const int TopCount = 10;

    static readonly Uri ValidationFailedProblemType = new("https://cloud-streams.io/problems/validation-failed");

    /// <inheritdoc/>
    public virtual async Task<IOperationResult<StreamActivity>> HandleAsync(GetEventStreamActivityQuery query, CancellationToken cancellationToken)
    {
        if (query.Period <= TimeSpan.Zero || query.Period > GetEventStreamActivityQuery.MaxPeriod) return Invalid($"The period must be positive and at most {GetEventStreamActivityQuery.MaxPeriod.TotalDays} days long");
        if (query.Buckets < 1 || query.Buckets > StreamActivity.MaxBuckets) return Invalid($"The amount of buckets must be between 1 and {StreamActivity.MaxBuckets}");
        if (query.SampleSize < 0 || query.SampleSize > StreamActivity.MaxSample) return Invalid($"The sample size must be between 0 and {StreamActivity.MaxSample}");
        var to = DateTimeOffset.UtcNow;
        var from = to - query.Period;
        var activity = new StreamActivity() { From = from, To = to };
        ulong length;
        try { length = (await eventStore.GetStreamMetadataAsync(cancellationToken).ConfigureAwait(false)).Length ?? 0; }
        catch (StreamNotFoundException) { length = 0; }
        var step = query.Period / query.Buckets;
        var bounds = new ulong[query.Buckets + 1];
        bounds[query.Buckets] = length;
        ulong low = 0;
        for (var i = 0; i < query.Buckets; i++)
        {
            bounds[i] = await this.FindOffsetAsync(from + step * i, low, length, cancellationToken).ConfigureAwait(false);
            low = bounds[i];
        }
        for (var i = 0; i < query.Buckets; i++)
        {
            activity.Buckets.Add(new()
            {
                From = from + step * i,
                To = i == query.Buckets - 1 ? to : from + step * (i + 1),
                Count = bounds[i + 1] > bounds[i] ? bounds[i + 1] - bounds[i] : 0
            });
        }
        if (query.SampleSize > 0 && length > 0)
        {
            var sources = new Dictionary<string, int>(StringComparer.Ordinal);
            var types = new Dictionary<string, int>(StringComparer.Ordinal);
            await foreach (var record in eventStore.ReadAsync(StreamReadDirection.Backwards, StreamPosition.EndOfStream, (ulong)query.SampleSize, cancellationToken).ConfigureAwait(false))
            {
                activity.SampleSize++;
                Count(sources, record, CloudEventAttributes.Source);
                Count(types, record, CloudEventAttributes.Type);
            }
            activity.Sources = Top(sources);
            activity.Types = Top(types);
        }
        return this.Ok(activity);
    }

    /// <summary>
    /// Finds the offset of the first cloud event recorded at or after the specified time, between the specified offsets
    /// </summary>
    /// <param name="time">The time to find the offset of</param>
    /// <param name="low">The lowest offset the cloud event may be at</param>
    /// <param name="high">The offset to return if no cloud event has been recorded at or after the specified time, which is to say the length of the stream</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The offset of the first cloud event recorded at or after the specified time, or <paramref name="high"/> if there is none</returns>
    /// <remarks>A record whose timestamp is unknown is deemed recorded at or after the time, like <see cref="ICloudEventStore.FindOffsetAsync"/> does</remarks>
    protected virtual async Task<ulong> FindOffsetAsync(DateTimeOffset time, ulong low, ulong high, CancellationToken cancellationToken)
    {
        while (low < high)
        {
            var middle = low + (high - low) / 2;
            var record = await eventStore.ReadAsync(StreamReadDirection.Forwards, (long)middle, 1, cancellationToken).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            if (record?.Timestamp == null || record.Timestamp >= time) high = middle;
            else low = middle + 1;
        }
        return low;
    }

    static void Count(Dictionary<string, int> counts, CloudEventRecord record, string attribute)
    {
        if (!record.Metadata.ContextAttributes.TryGetValue(attribute, out var value) || value?.ToString() is not { Length: > 0 } text) return;
        counts[text] = counts.TryGetValue(text, out var count) ? count + 1 : 1;
    }

    static List<StreamActivityCount> Top(Dictionary<string, int> counts) => [.. counts
        .OrderByDescending(kvp => kvp.Value)
        .ThenBy(kvp => kvp.Key, StringComparer.Ordinal)
        .Take(TopCount)
        .Select(kvp => new StreamActivityCount() { Value = kvp.Key, Count = kvp.Value })];

    static OperationResult<StreamActivity> Invalid(string detail) => new((int)HttpStatusCode.BadRequest, errors: [new Error(ValidationFailedProblemType, "Validation Failed", (int)HttpStatusCode.BadRequest, detail)]);

}
