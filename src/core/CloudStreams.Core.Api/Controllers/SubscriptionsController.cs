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

using CloudStreams.Core.Application.Commands.Subscriptions;
using CloudStreams.Core.Application.Queries.Subscriptions;

namespace CloudStreams.Core.Api.Controllers;

/// <summary>
/// Represents the <see cref="ResourceApiController{TResource}"/> used to manage <see cref="Subscription"/>s
/// </summary>
/// <inheritdoc/>
[Route("api/resources/v1/subscriptions")]
public class SubscriptionsController(IMediator mediator)
    : ClusterResourceApiController<Subscription>(mediator)
{

    /// <summary>
    /// Describes the health of all subscriptions matching the specified filters, including their lag, in a flat format suited to monitoring tools such as Grafana
    /// </summary>
    /// <param name="namespace">The namespace the subscriptions to describe belong to, if any. Subscriptions are cluster-scoped, so it is usually omitted</param>
    /// <param name="labelSelector">A comma-separated list of label selectors used to filter the subscriptions to describe, if any (e.g. <c>app=content,env=prod</c>)</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/> whose body is a JSON array at root level, empty if no subscription matches</returns>
    /// <remarks>The <c>health</c> segment takes precedence over the name of a subscription, so a subscription named 'health' cannot be retrieved with <c>GET {name}</c>; use the list endpoints instead</remarks>
    [HttpGet("health")]
    [ProducesResponseType(typeof(IEnumerable<SubscriptionHealth>), (int)HttpStatusCode.OK)]
    [ProducesErrorResponseType(typeof(Neuroglia.ProblemDetails))]
    public virtual async Task<IActionResult> GetSubscriptionsHealth([FromQuery(Name = "namespace")] string? @namespace = null, string? labelSelector = null, CancellationToken cancellationToken = default)
    {
        if (!this.TryParseLabelSelectors(labelSelector, out var labelSelectors)) return this.InvalidLabelSelector(labelSelector!);
        return this.Process(await this.Mediator.ExecuteAsync(new GetSubscriptionsHealthQuery(@namespace, labelSelectors), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Exports the specified subscription
    /// </summary>
    /// <param name="name">The name of the resource to get</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/></returns>
    [HttpGet("{name}/export")]
    [ProducesResponseType(typeof(IAsyncEnumerable<Resource>), (int)HttpStatusCode.OK)]
    [ProducesErrorResponseType(typeof(Neuroglia.ProblemDetails))]
    public virtual async Task<IActionResult> ExportSubscription(string name, CancellationToken cancellationToken = default)
    {
        var stream = (await this.Mediator.ExecuteAsync(new ExportSubscriptionCommand(name), cancellationToken).ConfigureAwait(false)).Data!;
        return this.File(stream, "application/x-yaml", $"{name}.yaml");
    }

}