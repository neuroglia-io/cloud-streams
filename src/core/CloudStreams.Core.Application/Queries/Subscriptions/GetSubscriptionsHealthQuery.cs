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
using Microsoft.Extensions.Logging;

namespace CloudStreams.Core.Application.Queries.Subscriptions;

/// <summary>
/// Represents the query used to describe the health of all matching <see cref="Subscription"/>s
/// </summary>
/// <param name="namespace">The namespace the <see cref="Subscription"/>s to describe belong to, if any</param>
/// <param name="labelSelectors">An <see cref="IEnumerable{T}"/> containing the <see cref="LabelSelector"/>s used to filter the <see cref="Subscription"/>s to describe, if any</param>
public class GetSubscriptionsHealthQuery(string? @namespace, IEnumerable<LabelSelector>? labelSelectors)
    : Query<IEnumerable<SubscriptionHealth>>
{

    /// <summary>
    /// Gets the namespace the <see cref="Subscription"/>s to describe belong to, if any
    /// </summary>
    public string? Namespace { get; } = @namespace;

    /// <summary>
    /// Gets an <see cref="IEnumerable{T}"/> containing the <see cref="LabelSelector"/>s used to filter the <see cref="Subscription"/>s to describe, if any
    /// </summary>
    public IEnumerable<LabelSelector>? LabelSelectors { get; } = labelSelectors;

}

/// <summary>
/// Represents the service used to handle <see cref="GetSubscriptionsHealthQuery"/> instances
/// </summary>
/// <param name="logger">The service used to perform logging</param>
/// <param name="repository">The service used to manage resources</param>
/// <param name="eventStore">The service used to store cloud events</param>
public class GetSubscriptionsHealthQueryHandler(ILogger<GetSubscriptionsHealthQueryHandler> logger, IResourceRepository repository, ICloudEventStore eventStore)
    : IQueryHandler<GetSubscriptionsHealthQuery, IEnumerable<SubscriptionHealth>>
{

    /// <inheritdoc/>
    public virtual async Task<IOperationResult<IEnumerable<SubscriptionHealth>>> HandleAsync(GetSubscriptionsHealthQuery query, CancellationToken cancellationToken)
    {
        var ranges = new Dictionary<string, (ulong? Length, ulong? FirstOffset)>();
        var results = new List<SubscriptionHealth>();
        await foreach (var subscription in repository.GetAllAsync<Subscription>(query.Namespace, query.LabelSelectors, cancellationToken).ConfigureAwait(false))
        {
            var partition = subscription.Spec.Partition;
            var streamKey = partition == null ? string.Empty : $"{EnumHelper.Stringify(partition.Type)}:{partition.Id}";
            if (!ranges.TryGetValue(streamKey, out var range))
            {
                range = await this.GetRangeAsync(partition, cancellationToken).ConfigureAwait(false);
                ranges[streamKey] = range;
            }
            var length = range.Length;
            var ackedOffset = subscription.Spec.Stream == null ? null : subscription.Status?.Stream?.AckedOffset;
            // the position of a subscription that has acknowledged nothing yet is the offset it starts from
            var position = ackedOffset ?? (subscription.Spec.Stream?.Offset >= 0 ? (ulong)subscription.Spec.Stream.Offset.Value : null);
            var fault = subscription.Status?.Stream?.Fault;
            var subscriberState = subscription.Status?.Subscriber?.State;
            results.Add(new()
            {
                Name = subscription.GetName(),
                Namespace = subscription.GetNamespace(),
                Phase = fault != null ? SubscriptionHealth.FailedPhase : EnumHelper.Stringify(subscription.Status?.Phase ?? SubscriptionStatusPhase.Inactive),
                PartitionId = partition?.Id,
                AckedOffset = ackedOffset,
                PartitionLength = length,
                Lag = ackedOffset.HasValue && length.HasValue ? (length.Value > ackedOffset.Value ? length.Value - ackedOffset.Value : 0) : null,
                SubscriberState = subscriberState == null || subscriberState == SubscriberState.Unknown ? null : EnumHelper.Stringify(subscriberState.Value),
                SubscriberReason = fault != null ? fault.Detail ?? fault.Title : subscription.Status?.Subscriber?.Reason,
                ExpiredEvents = position.HasValue && range.FirstOffset > position.Value ? range.FirstOffset.Value - position.Value : null
            });
        }
        return this.Ok(results);
    }

    /// <summary>
    /// Gets the length of the specified partition or, if none, of the whole cloud event stream, and the offset of the first event it retains (see #206)
    /// </summary>
    /// <param name="partition">The partition to get the range of, if any</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The length of the specified partition or stream, and the offset of its first retained event, both null if not available, e.g. because no event has been published to it yet</returns>
    protected virtual async Task<(ulong? Length, ulong? FirstOffset)> GetRangeAsync(PartitionReference? partition, CancellationToken cancellationToken)
    {
        try
        {
            if (partition == null)
            {
                var metadata = await eventStore.GetStreamMetadataAsync(cancellationToken).ConfigureAwait(false);
                return (metadata.Length, metadata.FirstOffset);
            }
            var partitionMetadata = await eventStore.GetPartitionMetadataAsync(partition, cancellationToken).ConfigureAwait(false);
            return (partitionMetadata.Length, partitionMetadata.FirstOffset);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug("Failed to get the length of the {stream}: {ex}", partition == null ? "cloud event stream" : $"partition '{partition.Id}'", ex.Message);
            return (null, null);
        }
    }

}
