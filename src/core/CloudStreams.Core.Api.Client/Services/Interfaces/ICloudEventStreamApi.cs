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

using CloudStreams.Core.Data;

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
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IAsyncEnumerable{T}"/> containing all the <see cref="CloudEvent"/>s read from the gateway's stream</returns>
    Task<IAsyncEnumerable<CloudEvent?>> ReadStreamAsync(StreamReadOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the metadata used to describe the gateway's <see cref="CloudEvent"/> stream
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="StreamMetadata"/> used to describe the gateway's <see cref="CloudEvent"/> stream</returns>
    Task<StreamMetadata> GetStreamMetadataAsync(CancellationToken cancellationToken = default);

}
