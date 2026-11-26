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
/// Defines the fundamentals of the Cloud Streams API used to manage <see cref="CloudEvent"/>s
/// </summary>
public interface ICloudEventsApi
{
    
    /// <summary>
    /// Gets the API used to manage <see cref="CloudEvent"/> partitions
    /// </summary>
    ICloudEventPartitionsApi Partitions { get; }

    /// <summary>
    /// Gets the API used to manage the gateway's <see cref="CloudEvent"/> stream
    /// </summary>
    ICloudEventStreamApi Stream { get; }

    /// <summary>
    /// Searches the cloud event stream, or a partition
    /// </summary>
    /// <param name="options">The options of the search</param>
    /// <param name="namespace">The namespace whose cloud events to read, if any. The partitions of the default namespace, and the whole stream, are read if not set</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The records found, and the offset to continue the search from</returns>
    Task<CloudEventSearchResult> SearchAsync(CloudEventSearchOptions options, string? @namespace = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes the specified cloud event, as the calling user, through the gateway's admission pipeline
    /// </summary>
    /// <param name="e">The cloud event to publish</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>An object that describes what became of the cloud event</returns>
    Task<CloudEventPublication> PublishAsync(CloudEvent e, CancellationToken cancellationToken = default);

}
