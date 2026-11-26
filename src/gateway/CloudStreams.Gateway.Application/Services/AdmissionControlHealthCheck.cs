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

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CloudStreams.Gateway.Application.Services;

/// <summary>
/// Represents the health check that tells whether the gateway has loaded its configuration, and can therefore admit cloud events (see #201)
/// </summary>
/// <param name="configuration">The service that holds the gateway's configuration</param>
public class AdmissionControlHealthCheck(IGatewayConfiguration configuration)
    : IHealthCheck
{

    /// <inheritdoc/>
    public Task<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) => Task.FromResult(configuration.IsReady
        ? Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy()
        : Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy("The gateway's configuration has not been loaded yet: cloud events are refused"));

}
