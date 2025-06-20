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

using CloudStreams.Core.Api.Client.Services;
using CloudStreams.Dashboard.Components.ResourceManagement;

namespace CloudStreams.Dashboard.Pages.Subscriptions.List;

/// <summary>
/// Represents the store used to manage a list of <see cref="Subscription"/>s
/// </summary>
/// <param name="logger">The service used to perform logging</param>
/// <param name="resourceManagementApi">The service used to interact with the Cloud Streams Resource management API</param>
/// <param name="resourceEventHub">The <see cref="IResourceEventWatchHub"/> websocket service client</param>
public class SubscriptionListStore(ILogger<SubscriptionListStore> logger, ICloudStreamsCoreApiClient resourceManagementApi, ResourceWatchEventHubClient resourceEventHub)
    : ResourceManagementComponentStore<SubscriptionListState, Subscription>(logger, resourceManagementApi, resourceEventHub)
{

    /// <summary>
    /// Gets an <see cref="IObservable{T}"/> used to observe <see cref="SubscriptionListState.GlobalStreamLength"/> changes
    /// </summary>
    protected IObservable<ulong> GlobalStreamLength => this.Select(state => state.GlobalStreamLength).DistinctUntilChanged();

    /// <summary>
    /// Gets an <see cref="IObservable{T}"/> used to observe <see cref="SubscriptionListState.PartitionLengths"/> changes
    /// </summary>
    protected IObservable<EquatableDictionary<string, ulong>> PartitionLengths => this.Select(state => state.PartitionLengths).DistinctUntilChanged();

    /// <summary>
    /// Gets an <see cref="IObservable{T}"/> used to observe <see cref="IResource"/>s of the specified type
    /// </summary>
    public IObservable<EquatableDictionary<string, ulong>> SubscriptionLengths => Observable.CombineLatest(
            InternalResources,
            SearchTerm.Throttle(TimeSpan.FromMilliseconds(100)).StartWith(""),
            GlobalStreamLength,
            PartitionLengths,
            (resources, searchTerm, globalStreamLength, partitionLengths) =>
            {
                if (resources == null)
                {
                    return [];
                }
                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    resources = [..resources.Where(r => r.GetName().Contains(searchTerm))];
                }
                return new EquatableDictionary<string, ulong>([.. 
                    resources.ToDictionary(r => 
                        r.GetName(), 
                        r =>
                        {
                            if (r.Spec?.Partition == null || string.IsNullOrEmpty(r.Spec.Partition.Id))
                            {
                                return globalStreamLength;
                            }
                            var key = GetPartitionKey(r.Spec.Partition.Type, r.Spec.Partition.Id);
                            if (!partitionLengths.ContainsKey(key)) return (ulong)0;
                            return partitionLengths[key];
                        }
                    )
                ]);
            }
         )
        .DistinctUntilChanged();

    /// <inheritdoc/>
    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        InternalResources.SubscribeAsync(async subscriptions =>
        {
            if (subscriptions == null || subscriptions.Count < 0) return;
            var partitions = new Dictionary<string, (CloudEventPartitionType, string)>();
            foreach(var subscription in subscriptions.Where(s => s.Spec?.Partition?.Type != null && !string.IsNullOrEmpty(s.Spec?.Partition?.Id)))
            {
                var key = GetPartitionKey(subscription.Spec.Partition!.Type, subscription.Spec.Partition.Id);
                if (!partitions.ContainsKey(key))
                {
                    partitions[key] = (subscription.Spec.Partition.Type, subscription.Spec.Partition.Id);
                }
            }
            await GetStreamsMetadata(partitions);
        }, CancellationTokenSource.Token);
    }
    private async Task GetStreamsMetadata(Dictionary<string, (CloudEventPartitionType, string)> partitions)
    {
        ulong globalStreamLength = 0;
        try
        {
            StreamMetadata metadata = await ResourceManagementApi.CloudEvents.Stream.GetStreamMetadataAsync(this.CancellationTokenSource.Token).ConfigureAwait(false);
            globalStreamLength = metadata?.Length ?? 0;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to get global stream metadata");
        }
        var partitionLengths = new EquatableDictionary<string, ulong>(Get().PartitionLengths);
        foreach (var kvp in partitions)
        {
            var key = kvp.Key;
            var (partitionType, partitionId) = kvp.Value;
            try
            {
                PartitionMetadata? metadata = await ResourceManagementApi.CloudEvents.Partitions.GetPartitionMetadataAsync(partitionType, partitionId!, CancellationTokenSource.Token).ConfigureAwait(false);
                partitionLengths[key] = metadata?.Length ?? 0;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to get metadata for partition {PartitionType}:{Partition}", partitionType, partitionId);
                partitionLengths[key] = 0;
            }
        }
        Reduce(state => state with
        {
            GlobalStreamLength = globalStreamLength,
            PartitionLengths = partitionLengths
        });
    }

    private string GetPartitionKey(CloudEventPartitionType? type, string? id) => $"{type}:{id}";
}
