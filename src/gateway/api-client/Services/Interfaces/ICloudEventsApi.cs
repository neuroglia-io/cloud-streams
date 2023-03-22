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

using CloudStreams.Core.Data.Models;

namespace CloudStreams.Gateway.Api.Client.Services;

/// <summary>
/// Defines the fundamentals of the Cloud Streams gateway API used to manage <see cref="CloudEvent"/>s
/// </summary>
public interface ICloudEventsApi
{

    /// <summary>
    /// Publishes the specified <see cref="CloudEvent"/> to the Cloud Streams gateway
    /// </summary>
    /// <param name="e">The <see cref="CloudEvent"/> to publish</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    Task PublishCloudEventAsync(CloudEvent e, CancellationToken cancellationToken = default);

}
