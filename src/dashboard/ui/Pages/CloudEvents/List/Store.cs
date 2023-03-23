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
using CloudStreams.Core.Data.Models;
using CloudStreams.Dashboard.StateManagement;
using CloudStreams.Gateway.Api.Client.Services;
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
    /// Gets an <see cref="IObservable{T}"/> used to observe <see cref="CloudEventListState.CloudEvents"/> changes
    /// </summary>
    public IObservable<List<CloudEvent>?> CloudEvents => this.Select(state => state.CloudEvents).DistinctUntilChanged();

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
            ReadOptions = readOptions,
            CloudEvents = new()
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
    /// Updates the <see cref="CloudEventListState.CloudEvents"/> when the <see cref="CloudEventHubClient"/> emits a new values
    /// </summary>
    /// <param name="e"></param>
    protected void OnCloudEventIngested(CloudEvent e)
    {
        List<CloudEvent> cloudEvents = new(this.Get(state => state.CloudEvents) ?? new());
        if (this.Get(state => state.ReadOptions).Direction == StreamReadDirection.Backwards) cloudEvents.Insert(0, e);
        else cloudEvents.Add(e);
        this.Reduce(state =>
        {
            return state with
            {
                CloudEvents = cloudEvents
            };
        });
    }

    /// <summary>
    /// Provides items to the <see cref="Virtualize{TItem}"/> component
    /// </summary>
    /// <param name="request">The <see cref="ItemsProviderRequest"/> to execute</param>
    /// <returns>The resulting <see cref="ItemsProviderResult{TResult}"/></returns>
    public async ValueTask<ItemsProviderResult<CloudEvent>> ProvideCloudEvents(ItemsProviderRequest request)
    {
        // Unstable
        //
        // ? Keep previous start index, difference with current one, negative-> scroll up, positive -> scroll down
        // ? what algo to fetch up and down accrodingly ?
        // ? when returning, instead of total count, either events.count+1?
        StreamReadOptions readOptions = this.Get(state => state.ReadOptions);
        if (readOptions == null)
        {
            return new ItemsProviderResult<CloudEvent>(new List<CloudEvent>(), 0);
        }
        this.SetLoading(true);
        await Task.Delay(1);
        readOptions = readOptions with { };
        int totalCount = (int?)this.Get(state => state.TotalCount) ?? 100;
        List<CloudEvent> cloudEvents = this.Get(state => state.CloudEvents) ?? new();
        if (cloudEvents.Any())
        {
            if (cloudEvents.Count() >= (request.StartIndex + request.Count))
            {
                this.SetLoading(false);
                return new ItemsProviderResult<CloudEvent>(cloudEvents.Skip(request.StartIndex).Take(request.Count), totalCount);
            }
            else
            {
                readOptions.Offset = (long?)cloudEvents.Last().GetSequence();
            }
        }
        readOptions.Offset = readOptions.Offset ?? (readOptions.Direction == StreamReadDirection.Forwards ? 0 : -1);
        readOptions.Length = (ulong)(request.StartIndex + request.Count - cloudEvents.Count());
        List<CloudEvent> fetchedCloudEvents = new();
        if (readOptions.Length <= StreamReadOptions.MaxLength)
        {
            fetchedCloudEvents = await (await this.cloudStreamsApi.CloudEvents.Stream.ReadStreamAsync(readOptions, this.CancellationTokenSource.Token).ConfigureAwait(false)).ToListAsync().ConfigureAwait(false) as List<CloudEvent>;
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
                var tempCloudEvents = await (await this.cloudStreamsApi.CloudEvents.Stream.ReadStreamAsync(tempReadOptions, this.CancellationTokenSource.Token).ConfigureAwait(false)).ToListAsync().ConfigureAwait(false) as List<CloudEvent>;
                fetchedCloudEvents.AddRange(tempCloudEvents);
                offset = (long)fetchedCloudEvents.Last()!.GetSequence()!;
                fetchMore = tempCloudEvents.Count() > 1 && (ulong)fetchedCloudEvents.Count < readOptions!.Length;
            }
            while (fetchMore);
        }
        cloudEvents.AddRange(fetchedCloudEvents);
        this.Reduce(state => state with 
        { 
            CloudEvents = cloudEvents 
        });
        this.SetLoading(false);
        return new ItemsProviderResult<CloudEvent>(cloudEvents.Skip(request.StartIndex).Take(request.Count), totalCount);
    }
}
