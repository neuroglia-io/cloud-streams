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

using CloudStreams.Core.Api.Configuration;
using Microsoft.Extensions.Options;

namespace CloudStreams.Core.Api.Services;

/// <summary>
/// Represents the service that warns, at startup, about configurations that leave Cloud Streams exposed
/// </summary>
/// <param name="logger">The service used to perform logging</param>
/// <param name="authentication">The current <see cref="CloudStreamsAuthenticationOptions"/></param>
/// <param name="managementApi">The current <see cref="ManagementApiOptions"/></param>
/// <param name="configuration">The current <see cref="IConfiguration"/></param>
/// <remarks>Relaxing security is a legitimate choice, for instance behind an ingress that authenticates requests, but it must never go unnoticed (see #144 and #145)</remarks>
public class SecurityConfigurationAuditor(ILogger<SecurityConfigurationAuditor> logger, CloudStreamsAuthenticationOptions authentication, IOptions<ManagementApiOptions> managementApi, IConfiguration configuration)
    : IHostedService
{

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var warning in this.GetWarnings()) logger.LogWarning("SECURITY: {warning}", warning);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Gets the warnings about the current configuration
    /// </summary>
    /// <returns>A new <see cref="IEnumerable{T}"/> containing the warnings about the current configuration</returns>
    public virtual IEnumerable<string> GetWarnings()
    {
        var api = managementApi.Value;
        if (api.Enabled && !authentication.Dashboard.Enabled)
        {
            var where = api.Port.HasValue ? $"port {api.Port}" : "the same port as the public endpoints, such as event ingestion";
            yield return $"The management API, which allows anyone to create, change and delete subscriptions, brokers and gateways, is served without authentication on {where}. " +
                $"Unless an ingress authenticates requests, enable '{CloudStreamsAuthenticationOptions.ConfigurationSection}:Dashboard:Enabled', " +
                $"or serve the management API on a port that is not publicly exposed with '{ManagementApiOptions.ConfigurationSection}:Port'";
        }
        if (!string.IsNullOrWhiteSpace(authentication.Oidc.Authority) && string.IsNullOrWhiteSpace(authentication.Oidc.Audience))
        {
            yield return $"Bearer tokens are accepted without validating their audience: any token issued by '{authentication.Oidc.Authority}', for any client, is accepted. " +
                $"Set '{CloudStreamsAuthenticationOptions.ConfigurationSection}:Oidc:Audience', and '{CloudStreamsAuthenticationOptions.ConfigurationSection}:Oidc:RequireAudience' to enforce it";
        }
        if (configuration.GetValue<bool>("ForwardedHeaders:TrustAllProxies"))
        {
            yield return "Forwarded headers are trusted from any client ('ForwardedHeaders:TrustAllProxies'): clients that can reach the application without going through a proxy can spoof their address and scheme";
        }
    }

}
