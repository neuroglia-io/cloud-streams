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


using Neuroglia.Data.Infrastructure.ResourceOriented.Services;
using Neuroglia.Serialization;

namespace CloudStreams.Core.Api.Services;

/// <summary>
/// Represents a service used to monitor the health of a gateway
/// </summary>
/// <param name="logger">The service used to perform logging</param>
/// <param name="serializer">The service used to serialize/deserialize objects to/from JSON</param>
/// <param name="repository">The service used to manage resources</param>
/// <param name="monitor">The service used to monitor the handled <see cref="Resources.Gateway"/></param>
/// <param name="httpClient">The service used to perform HTTP requests</param>
public class GatewayHealthMonitor(ILogger<GatewayHealthMonitor> logger, IJsonSerializer serializer, IResourceRepository repository, IResourceMonitor<Gateway> monitor, HttpClient httpClient)
    : ServiceHealthMonitor<Gateway>(logger, serializer, repository, monitor, httpClient)
{

    /// <inheritdoc/>
    protected override ServiceConfiguration? GetService(Gateway resource) => resource.Spec.Service;

    /// <inheritdoc/>
    protected override string? GetHealthStatus(Gateway resource) => resource.Status?.HealthStatus;

    /// <inheritdoc/>
    protected override DateTimeOffset? GetLastHealthCheckAt(Gateway resource) => resource.Status?.LastHealthCheckAt;

    /// <inheritdoc/>
    protected override void SetHealthStatus(Gateway resource, string healthStatus, DateTimeOffset checkedAt)
    {
        resource.Status ??= new();
        resource.Status.HealthStatus = healthStatus;
        resource.Status.LastHealthCheckAt = checkedAt;
    }

}
