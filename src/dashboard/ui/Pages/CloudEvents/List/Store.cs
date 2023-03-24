// Copyright © 2023-Present The Cloud Streams Authors
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
using CloudStreams.Dashboard.StateManagement;
using Microsoft.AspNetCore.Components.Web.Virtualization;
using System.Reactive.Linq;

namespace CloudStreams.Dashboard.Pages.CloudEvents.List;

/// <summary>
/// Represents the Cloud Event List's <see cref="ComponentStore{TState}"/>
/// </summary>
public class CloudEventListStore
    : ComponentStore<CloudEventListState>
{

    ICloudStreamsApiClient cloudStreamsApi;

    /// <summary>
    /// Initializes a new <see cref="CloudEventListStore"/>
    /// </summary>
    /// <param name="cloudStreamsApi">The service used to interact with the Cloud Streams Gateway API</param>
    public CloudEventListStore(ICloudStreamsApiClient cloudStreamsApi) 
        : base(new())
    { 
        this.cloudStreamsApi = cloudStreamsApi;
    }

    /// <summary>
    /// Gets an <see cref="IObservable{T}"/> used to observe <see cref="CloudEventListState.Loading"/> changes
    /// </summary>
    public IObservable<bool> Loading => this.Select(state => state.Loading).DistinctUntilChanged();

    /// <summary>
    /// Gets an <see cref="IObservable{T}"/> used to observe <see cref="CloudEventListState.ReadOptions"/> changes
    /// </summary>
    public IObservable<StreamReadOptions> ReadOptions => this.Select(state => {
        if (state.ReadOptions.Partition?.Type != null && state.ReadOptions.Partition?.Id != null)
        {
            return state.ReadOptions;
        }
        return state.ReadOptions with
        {
            Partition = null
        };
    }).DistinctUntilChanged();

    /// <inheritdoc/>
    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
    }

    /// <summary>
    /// Sets the <see cref="StreamReadOptions"/>
    /// </summary>
    /// <param name="readOptions">The new <see cref="StreamReadOptions"/></param>
    public void SetReadOptions(StreamReadOptions readOptions)
    {
        this.Reduce(state => state with
        {
            ReadOptions = readOptions
        });
    }

    /// <summary>
    /// Sets the <see cref="CloudEventListState.TotalCount"/>
    /// </summary>
    /// <param name="totalCount">The new total count</param>
    public void SetTotalCount(ulong? totalCount)
    {
        this.Reduce(state => state with
        {
            TotalCount = totalCount
        });
    }

    /// <summary>
    /// Sets the <see cref="CloudEventListState.Loading"/>
    /// </summary>
    /// <param name="loading">The new loading state</param>
    public void SetLoading(bool loading)
    {
        this.Reduce(state => state with
        {
            Loading = loading
        });
    }

    /// <summary>
    /// Provides items to the <see cref="Virtualize{TItem}"/> component
    /// </summary>
    /// <param name="request">The <see cref="ItemsProviderRequest"/> to execute</param>
    /// <returns>The resulting <see cref="ItemsProviderResult{TResult}"/></returns>
    public async ValueTask<ItemsProviderResult<CloudEvent>> ProvideCloudEvents(ItemsProviderRequest request)
    {
        StreamReadOptions readOptions = this.Get(state => state.ReadOptions);
        if (readOptions == null)
        {
            return new ItemsProviderResult<CloudEvent>(new List<CloudEvent>(), 0);
        }
        this.SetLoading(true);
        readOptions = readOptions with { };
        int totalCount = (int?)this.Get(state => state.TotalCount) ?? 100;
        if (readOptions.Direction == StreamReadDirection.Forwards)
        {
            readOptions.Offset = (readOptions.Offset ?? 0) + request.StartIndex;
        }
        else
        {
            if ((readOptions.Offset??0) == 0 && request.StartIndex == 0)
            {
                readOptions.Offset = -1;
            }
            else
            {
                readOptions.Offset = Math.Max((readOptions.Offset ?? totalCount) - request.StartIndex, 0);
            }
        }
        readOptions.Length = (ulong)request.Count;
        List<CloudEvent> fetchedCloudEvents = new();
        if (readOptions.Length <= StreamReadOptions.MaxLength)
        {
            fetchedCloudEvents = await (await this.cloudStreamsApi.CloudEvents.Stream.ReadStreamAsync(readOptions, request.CancellationToken).ConfigureAwait(false)).ToListAsync().ConfigureAwait(false) as List<CloudEvent>;
        }
        else
        {
            bool fetchMore = true;
            long offset = readOptions.Offset.Value;
            do
            {
                StreamReadOptions tempReadOptions = readOptions with { };
                tempReadOptions.Offset = offset;
                tempReadOptions.Length = StreamReadOptions.MaxLength;
                var tempCloudEvents = await (await this.cloudStreamsApi.CloudEvents.Stream.ReadStreamAsync(tempReadOptions, request.CancellationToken).ConfigureAwait(false)).ToListAsync().ConfigureAwait(false) as List<CloudEvent>;
                fetchedCloudEvents.AddRange(tempCloudEvents);
                offset = (long)fetchedCloudEvents.Last()!.GetSequence()!;
                fetchMore = tempCloudEvents.Count() > 1 && (ulong)fetchedCloudEvents.Count < readOptions!.Length;
            }
            while (fetchMore);
        }
        this.SetLoading(false);
        return new ItemsProviderResult<CloudEvent>(fetchedCloudEvents, totalCount);
    }
}
