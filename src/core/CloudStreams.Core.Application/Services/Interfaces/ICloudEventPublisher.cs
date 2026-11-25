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

using System.Security.Claims;

namespace CloudStreams.Core.Application.Services;

/// <summary>
/// Defines the fundamentals of a service used to publish cloud events through the gateway's admission pipeline, on behalf of a user
/// </summary>
/// <remarks>Only gateways ingest cloud events: the service is not available elsewhere, e.g. in the standalone API</remarks>
public interface ICloudEventPublisher
{

    /// <summary>
    /// Publishes the specified cloud event, as if the specified user had posted it to the ingestion endpoint
    /// </summary>
    /// <param name="e">The cloud event to publish</param>
    /// <param name="user">The user to publish the cloud event on behalf of</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The result of the ingestion: <c>201</c> if the cloud event has been stored, <c>202</c> if it has been ignored, <c>200</c> if it is a duplicate</returns>
    Task<IOperationResult> PublishAsync(CloudEvent e, ClaimsPrincipal user, CancellationToken cancellationToken = default);

}
