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

using CloudStreams.Core.Resources;
using Neuroglia;
using Neuroglia.Data.Infrastructure.ResourceOriented;
using Neuroglia.Data.Infrastructure.ResourceOriented.Properties;
using Neuroglia.Data.Infrastructure.ResourceOriented.Services;
using Neuroglia.Mediation;
using Neuroglia.Serialization;
using System.Diagnostics;
using System.Net;
using System.Net.Mime;
using System.Text;
using System.Text.Json.Nodes;

namespace CloudStreams.Core.Api.Queries.Gateways;

/// <summary>
/// Represents the <see cref="IQuery{TResult}"/> used to check the health of a <see cref="Data.Gateway"/>
/// </summary>
public class CheckGatewayHealthQuery
    : Query<HealthCheckResponse?>
{

    /// <summary>
    /// Initializes a new <see cref="CheckGatewayHealthQuery"/>
    /// </summary>
    protected CheckGatewayHealthQuery() { }

    /// <summary>
    /// Initializes a new <see cref="CheckGatewayHealthQuery"/>
    /// </summary>
    /// <param name="name">The name of the gateway to check the health of</param>
    public CheckGatewayHealthQuery(string name)
    {
        this.Name = name;
    }

    /// <summary>
    /// Gets the name of the gateway to check the health of
    /// </summary>
    public virtual string Name { get; protected set; } = null!;

}

/// <summary>
/// Represents the service used to handle <see cref="CheckGatewayHealthQuery"/> instances
/// </summary>
public class CheckGatewayHealthQueryHandler(IRepository repository, HttpClient httpClient, IJsonSerializer serializer)
    : IQueryHandler<CheckGatewayHealthQuery, HealthCheckResponse?>
{

    /// <inheritdoc/>
    public virtual async Task<IOperationResult<HealthCheckResponse?>> HandleAsync(CheckGatewayHealthQuery query, CancellationToken cancellationToken)
    {
        var gateway = await repository.GetAsync<Gateway>(query.Name, null, cancellationToken).ConfigureAwait(false);
        if (gateway == null) return this.NotFound();
        if (gateway.Spec.Service == null || gateway.Spec.Service.HealthChecks == null) return this.Ok(null);
        using var request = new HttpRequestMessage(new HttpMethod(gateway.Spec.Service.HealthChecks.Request.Method), $"{gateway.Spec.Service.Uri.OriginalString}{gateway.Spec.Service.HealthChecks.Request.Path}");
        if (gateway.Spec.Service.HealthChecks.Request.Headers != null) gateway.Spec.Service.HealthChecks.Request.Headers!.ToList().ForEach(h => request.Headers.TryAddWithoutValidation(h.Key, h.Value));
        if (gateway.Spec.Service.HealthChecks.Request.Body != null) request.Content = new StringContent(serializer.SerializeToText(gateway.Spec.Service.HealthChecks.Request.Body), Encoding.UTF8, MediaTypeNames.Application.Json);
        HealthCheckResponse? healthCheckResponse = null;
        try
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();
            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                try
                {
                    healthCheckResponse = serializer.Deserialize<HealthCheckResponse>(content)!;
                }
                catch { }
                if (healthCheckResponse == null)
                {
                    var result = serializer.Deserialize<JsonObject>(content);
                    if (result?.TryGetPropertyValue(nameof(HealthCheckResult.Status).ToCamelCase(), out var node) == true && node != null) healthCheckResponse = new(node.GetValue<string>());
                    else healthCheckResponse = new(response.IsSuccessStatusCode ? HealthStatus.Healthy : HealthStatus.Unhealthy);
                }
            }
            catch
            {
                healthCheckResponse = new(HealthStatus.Unhealthy);
            }
        }
        catch
        {
            healthCheckResponse = new(HealthStatus.Unhealthy);
        }
        return this.Ok(healthCheckResponse);
    }

}