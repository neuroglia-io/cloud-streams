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
using HealthCheckResult = Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult;

namespace CloudStreams.Operator.Services;

/// <summary>
/// Reports a replica of the operator as ready once it can serve the admission webhook, i.e. once its serving certificate is loaded
/// </summary>
/// <param name="provider">The service that loads the serving certificate</param>
/// <param name="options">The options of the operator</param>
public class WebhookCertificateHealthCheck(WebhookCertificateProvider provider, IOptions<OperatorOptions> options)
    : IHealthCheck
{

    /// <inheritdoc/>
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(!options.Value.Webhook.Enabled || provider.Certificate != null
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("The serving certificate of the admission webhook is not loaded yet"));

}
