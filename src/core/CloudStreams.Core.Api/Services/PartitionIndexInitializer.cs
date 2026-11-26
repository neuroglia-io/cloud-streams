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

namespace CloudStreams.Core.Api.Services;

/// <summary>
/// Represents the service that backfills the partition index, once, from the projections earlier versions used to list partition ids
/// </summary>
/// <param name="logger">The service used to perform logging</param>
/// <param name="partitionIndex">The index of the partitions cloud events have been published to</param>
/// <param name="eventStore">The service used to store cloud events</param>
/// <remarks>
/// Backfilling is idempotent and runs in the background, retrying with backoff, so that it neither delays startup nor gets lost when the event store is unavailable.
/// Once a type of partition has been backfilled, it is marked as such and never backfilled again: new partitions are indexed as events are ingested (see #185)
/// </remarks>
public class PartitionIndexInitializer(ILogger<PartitionIndexInitializer> logger, IPartitionIndex partitionIndex, ICloudEventStore eventStore)
    : BackgroundService
{

    const int BatchSize = 1000;

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        for (var attempt = 1; !stoppingToken.IsCancellationRequested; attempt++)
        {
            try
            {
                foreach (var partitionType in Enum.GetValues<CloudEventPartitionType>()) await this.BackfillAsync(partitionType, stoppingToken).ConfigureAwait(false);
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                var delay = TimeSpan.FromSeconds(Math.Min(5 * Math.Pow(2, attempt - 1), 300));
                logger.LogError(ex, "Failed to backfill the partition index. Retrying in {delay}...", delay);
                try { await Task.Delay(delay, stoppingToken).ConfigureAwait(false); }
                catch (OperationCanceledException) { return; }
            }
        }
    }

    /// <summary>
    /// Backfills the index of the specified type of partitions, unless it has been backfilled already
    /// </summary>
    /// <param name="partitionType">The type of partitions to backfill the index of</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    protected virtual async Task BackfillAsync(CloudEventPartitionType partitionType, CancellationToken cancellationToken)
    {
        if (await partitionIndex.IsInitializedAsync(partitionType, cancellationToken).ConfigureAwait(false)) return;
        var count = 0;
        var batch = new List<PartitionReference>(BatchSize);
        await foreach (var id in eventStore.ListPartitionIdsAsync(partitionType, cancellationToken).ConfigureAwait(false))
        {
            batch.Add(new(partitionType, id));
            if (batch.Count < BatchSize) continue;
            await partitionIndex.AddAsync(Namespace.DefaultNamespaceName, batch, cancellationToken).ConfigureAwait(false);
            count += batch.Count;
            batch.Clear();
        }
        if (batch.Count > 0) await partitionIndex.AddAsync(Namespace.DefaultNamespaceName, batch, cancellationToken).ConfigureAwait(false);
        count += batch.Count;
        await partitionIndex.MarkInitializedAsync(partitionType, cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Backfilled the index of the partitions of type '{partitionType}' with {count} id(s) from the projections of earlier versions", EnumHelper.Stringify(partitionType), count);
    }

}
