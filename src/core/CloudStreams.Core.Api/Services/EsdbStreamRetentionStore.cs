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
using EventStore.Client;
using EsdbStreamMetadata = EventStore.Client.StreamMetadata;

namespace CloudStreams.Core.Api.Services;

/// <summary>
/// Represents the <see cref="IStreamRetentionStore"/> that keeps the retention policy of the cloud event stream in its EventStoreDB metadata, as <c>$maxAge</c> and
/// <c>$maxCount</c>, which EventStoreDB enforces itself (see #206)
/// </summary>
/// <param name="client">The client used to read and write the metadata of the cloud event stream</param>
public class EsdbStreamRetentionStore(EventStoreClient client)
    : IStreamRetentionStore
{

    /// <inheritdoc/>
    public virtual async Task<StreamRetention?> GetAsync(CancellationToken cancellationToken = default)
    {
        var result = await client.GetStreamMetadataAsync(StreamNameHelper.All, cancellationToken: cancellationToken).ConfigureAwait(false);
        var metadata = result.Metadata;
        if (metadata.MaxAge == null && metadata.MaxCount == null) return null;
        return new()
        {
            MaxAge = metadata.MaxAge.HasValue ? Duration.FromTimeSpan(metadata.MaxAge.Value) : null,
            MaxCount = metadata.MaxCount.HasValue ? (ulong)metadata.MaxCount.Value : null
        };
    }

    /// <inheritdoc/>
    /// <remarks>The rest of the stream's metadata, such as its access control list or the position it has been truncated before, is kept</remarks>
    public virtual async Task SetAsync(StreamRetention retention, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(retention);
        var current = (await client.GetStreamMetadataAsync(StreamNameHelper.All, cancellationToken: cancellationToken).ConfigureAwait(false)).Metadata;
        var maxAge = retention.MaxAge?.ToTimeSpan() is TimeSpan age && age > TimeSpan.Zero ? age : (TimeSpan?)null;
        var maxCount = retention.MaxCount is ulong count && count > 0 ? (int)Math.Min(count, int.MaxValue) : (int?)null;
        var metadata = new EsdbStreamMetadata(maxCount, maxAge, current.TruncateBefore, current.CacheControl, current.Acl, current.CustomMetadata);
        await client.SetStreamMetadataAsync(StreamNameHelper.All, StreamState.Any, metadata, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

}
