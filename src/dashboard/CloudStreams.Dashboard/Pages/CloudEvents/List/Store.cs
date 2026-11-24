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
using Microsoft.AspNetCore.Components.Web.Virtualization;

namespace CloudStreams.Dashboard.Pages.CloudEvents.List;

/// <summary>
/// Represents the Cloud Event List's <see cref="ComponentStore{TState}"/>
/// </summary>
/// <remarks>
/// Initializes a new <see cref="CloudEventListStore"/>
/// </remarks>
/// <param name="cloudStreamsApi">The service used to interact with the Cloud Streams Gateway API</param>
public class CloudEventListStore(ICloudStreamsCoreApiClient cloudStreamsApi)
        : ComponentStore<CloudEventListState>(new())
{

    ICloudStreamsCoreApiClient cloudStreamsApi = cloudStreamsApi;

    /// <summary>
    /// Gets an <see cref="IObservable{T}"/> used to observe <see cref="CloudEventListState.Loading"/> changes
    /// </summary>
    public IObservable<bool> Loading => this.Select(state => state.Loading).DistinctUntilChanged();

    /// <summary>
    /// Gets an <see cref="IObservable{T}"/> used to observe <see cref="CloudEventListState.TotalCount"/> changes
    /// </summary>
    public IObservable<ulong?> TotalCount => this.Select(state => state.TotalCount).DistinctUntilChanged();

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
    /// <remarks>
    /// Pages are read in chunks of at most <see cref="StreamReadOptions.MaxLength"/> events, each starting right after the last event of the previous one, so that no
    /// event is listed twice. Failures are logged and yield an empty page rather than breaking the page, and the loading state always ends (see #194)
    /// </remarks>
    public async ValueTask<ItemsProviderResult<CloudEvent>> ProvideCloudEvents(ItemsProviderRequest request)
    {
        var readOptions = this.Get(state => state.ReadOptions);
        if (readOptions == null) return new ItemsProviderResult<CloudEvent>([], 0);
        var totalCount = (int?)this.Get(state => state.TotalCount);
        if (totalCount == null) return new ItemsProviderResult<CloudEvent>([], 0);
        this.SetLoading(true);
        try
        {
            readOptions = readOptions with { };
            if (readOptions.Partition?.Type == null || readOptions.Partition?.Id == null) readOptions = readOptions with { Partition = null };
            if (readOptions.Direction == StreamReadDirection.Forwards)
            {
                readOptions.Offset = (readOptions.Offset is null or < 0 ? 0 : readOptions.Offset.Value) + request.StartIndex;
            }
            else if (readOptions.Offset is null or < 0)
            {
                // reading backwards from the end: the n-th item is the event at offset 'length - 1 - n'
                readOptions.Offset = request.StartIndex == 0 ? StreamPosition.EndOfStream : Math.Max(totalCount.Value - 1 - request.StartIndex, 0);
            }
            else
            {
                readOptions.Offset = Math.Max(readOptions.Offset.Value - request.StartIndex, 0);
            }
            readOptions.Length = (ulong)request.Count;
            var fetchedCloudEvents = new List<CloudEvent>();
            var offset = readOptions.Offset.Value;
            while ((ulong)fetchedCloudEvents.Count < readOptions.Length)
            {
                var chunkOptions = readOptions with
                {
                    Offset = offset,
                    Length = Math.Min(readOptions.Length - (ulong)fetchedCloudEvents.Count, (ulong)StreamReadOptions.MaxLength)
                };
                var chunk = await (await this.cloudStreamsApi.CloudEvents.Stream.ReadStreamAsync(chunkOptions, request.CancellationToken).ConfigureAwait(false)).ToListAsync(request.CancellationToken).ConfigureAwait(false);
                var events = chunk.OfType<CloudEvent>().ToList();
                fetchedCloudEvents.AddRange(events);
                if ((ulong)events.Count < chunkOptions.Length) break;
                var lastSequence = events[^1].GetSequence();
                if (lastSequence == null) break;
                if (readOptions.Direction == StreamReadDirection.Forwards) offset = (long)lastSequence.Value + 1;
                else if (lastSequence.Value == 0) break;
                else offset = (long)lastSequence.Value - 1;
            }
            return new ItemsProviderResult<CloudEvent>(fetchedCloudEvents, totalCount.Value);
        }
        catch (OperationCanceledException) when (request.CancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to read cloud events: {ex}");
            return new ItemsProviderResult<CloudEvent>([], totalCount.Value);
        }
        finally
        {
            this.SetLoading(false);
        }
    }
}
