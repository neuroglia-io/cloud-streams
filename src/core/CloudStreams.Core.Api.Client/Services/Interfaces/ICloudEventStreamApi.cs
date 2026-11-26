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

using Neuroglia.Eventing.CloudEvents;

namespace CloudStreams.Core.Api.Client.Services;

/// <summary>
/// Defines the fundamentals of the API used to manage a Cloud Streams gateway's <see cref="CloudEvent"/> stream 
/// </summary>
public interface ICloudEventStreamApi
{

    /// <summary>
    /// Reads the gateway's <see cref="CloudEvent"/> stream
    /// </summary>
    /// <param name="options">An object used to configure the read options</param>
    /// <param name="namespace">The namespace whose cloud events to read, if any. The partitions of the default namespace, and the whole stream, are read if not set</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IAsyncEnumerable{T}"/> containing all the <see cref="CloudEvent"/>s read from the gateway's stream</returns>
    Task<IAsyncEnumerable<CloudEvent?>> ReadStreamAsync(StreamReadOptions? options = null, string? @namespace = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the metadata used to describe the gateway's <see cref="CloudEvent"/> stream
    /// </summary>
    /// <param name="namespace">The namespace whose cloud events to read, if any. The partitions of the default namespace, and the whole stream, are read if not set</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="StreamMetadata"/> used to describe the gateway's <see cref="CloudEvent"/> stream</returns>
    Task<StreamMetadata> GetStreamMetadataAsync(string? @namespace = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Describes the recent activity of the gateway's <see cref="CloudEvent"/> stream
    /// </summary>
    /// <param name="period">The period, ending now, to describe. Defaults to 24 hours</param>
    /// <param name="buckets">The amount of consecutive slices to split the period into. Defaults to 24</param>
    /// <param name="sampleSize">The amount of most recent cloud events to compute the most active sources and types from. Defaults to 500</param>
    /// <param name="namespace">The namespace whose cloud events to read, if any. The partitions of the default namespace, and the whole stream, are read if not set</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="StreamActivity"/> that describes the recent activity of the stream</returns>
    Task<StreamActivity> GetStreamActivityAsync(TimeSpan? period = null, int? buckets = null, int? sampleSize = null, string? @namespace = null, CancellationToken cancellationToken = default);

}
