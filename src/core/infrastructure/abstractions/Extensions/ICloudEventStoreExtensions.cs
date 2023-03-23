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

using CloudStreams.Core.Infrastructure.Services;

namespace CloudStreams.Core.Infrastructure;

/// <summary>
/// Defines extensions for <see cref="ICloudEventStore"/>s
/// </summary>
public static class ICloudEventStoreExtensions
{

    /// <summary>
    /// Reads a single event from the stream
    /// </summary>
    /// <param name="events">The <see cref="ICloudEventStore"/> to read the event from</param>
    /// <param name="direction">The direction in which to read the stream</param>
    /// <param name="offset">The offset of the event to read</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The <see cref="CloudEventRecord"/> at the specified offset</returns>
    public static async Task<CloudEventRecord?> ReadOneAsync(this ICloudEventStore events, StreamReadDirection direction, long offset, CancellationToken cancellationToken = default)
    {
        return await events.ReadAsync(direction, offset, 1, cancellationToken).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

}
