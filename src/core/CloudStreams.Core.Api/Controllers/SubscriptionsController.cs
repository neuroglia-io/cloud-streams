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
using Microsoft.AspNetCore.Authorization;

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
    /// Acknowledges a cloud event whose acknowledgement the subscriber has deferred by responding '202 Accepted'. Delivery then resumes after it
    /// </summary>
    /// <param name="name">The name of the subscription the cloud event has been delivered for</param>
    /// <param name="request">An object that describes the acknowledgement</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/></returns>
    /// <remarks>The acknowledgement token is the authorization: the endpoint is therefore anonymous, regardless of how the dashboard is secured</remarks>
    [HttpPost("{name}/ack")]
    [AllowAnonymous]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    [ProducesResponseType(typeof(Neuroglia.ProblemDetails), (int)HttpStatusCode.BadRequest)]
    [ProducesResponseType(typeof(Neuroglia.ProblemDetails), (int)HttpStatusCode.Conflict)]
    public virtual async Task<IActionResult> AcknowledgeEvent(string name, [FromBody] AcknowledgementRequest request, CancellationToken cancellationToken = default)
    {
        if (!this.ModelState.IsValid) return this.ValidationProblem(this.ModelState);
        var result = await this.Mediator.ExecuteAsync(new AcknowledgeSubscriptionEventCommand(name, request.Token, true), cancellationToken).ConfigureAwait(false);
        return result.Status is >= 200 and < 300 ? this.NoContent() : this.Problem(result);
    }

    /// <summary>
    /// Negatively acknowledges a cloud event whose acknowledgement the subscriber has deferred by responding '202 Accepted'. The cloud event is then redelivered
    /// </summary>
    /// <param name="name">The name of the subscription the cloud event has been delivered for</param>
    /// <param name="request">An object that describes the negative acknowledgement</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/></returns>
    /// <remarks>The acknowledgement token is the authorization: the endpoint is therefore anonymous, regardless of how the dashboard is secured</remarks>
    [HttpPost("{name}/nack")]
    [AllowAnonymous]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    [ProducesResponseType(typeof(Neuroglia.ProblemDetails), (int)HttpStatusCode.BadRequest)]
    [ProducesResponseType(typeof(Neuroglia.ProblemDetails), (int)HttpStatusCode.Conflict)]
    public virtual async Task<IActionResult> NegativelyAcknowledgeEvent(string name, [FromBody] NegativeAcknowledgementRequest request, CancellationToken cancellationToken = default)
    {
        if (!this.ModelState.IsValid) return this.ValidationProblem(this.ModelState);
        var result = await this.Mediator.ExecuteAsync(new AcknowledgeSubscriptionEventCommand(name, request.Token, false, request.Reason), cancellationToken).ConfigureAwait(false);
        return result.Status is >= 200 and < 300 ? this.NoContent() : this.Problem(result);
    }

    /// <summary>
    /// Suspends the specified subscription: nothing is delivered, and its offset is kept, until it is resumed
    /// </summary>
    /// <param name="name">The name of the subscription to suspend</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/> whose body is the suspended subscription</returns>
    /// <remarks>Requires the operator role, which, unlike the admin role, does not allow patching the subscription (see #214)</remarks>
    [HttpPost("{name}/suspend")]
    [Authorize(Policy = CloudStreamsAuthenticationDefaults.OperatePolicy)]
    [ProducesResponseType(typeof(Subscription), (int)HttpStatusCode.OK)]
    [ProducesErrorResponseType(typeof(Neuroglia.ProblemDetails))]
    public virtual async Task<IActionResult> SuspendSubscription(string name, CancellationToken cancellationToken = default)
    {
        return this.Process(await this.Mediator.ExecuteAsync(new SuspendSubscriptionCommand(name), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Resumes the specified suspended or faulted subscription. A faulted subscription has its fault cleared, so that the cloud event that caused it is retried: to skip that event
    /// instead, change the subscription's offset
    /// </summary>
    /// <param name="name">The name of the subscription to resume</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/> whose body is the resumed subscription</returns>
    /// <remarks>Requires the operator role, which, unlike the admin role, does not allow patching the subscription's status (see #147)</remarks>
    [HttpPost("{name}/resume")]
    [Authorize(Policy = CloudStreamsAuthenticationDefaults.OperatePolicy)]
    [ProducesResponseType(typeof(Subscription), (int)HttpStatusCode.OK)]
    [ProducesErrorResponseType(typeof(Neuroglia.ProblemDetails))]
    public virtual async Task<IActionResult> ResumeSubscription(string name, CancellationToken cancellationToken = default)
    {
        return this.Process(await this.Mediator.ExecuteAsync(new ResumeSubscriptionCommand(name), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Replays the specified subscription, by delivering every cloud event matching it again, from the beginning of the stream
    /// </summary>
    /// <param name="name">The name of the subscription to replay</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/> whose body is the replayed subscription</returns>
    /// <remarks>Requires the operator role, which, unlike the admin role, does not allow patching the subscription (see #147)</remarks>
    [HttpPost("{name}/replay")]
    [Authorize(Policy = CloudStreamsAuthenticationDefaults.OperatePolicy)]
    [ProducesResponseType(typeof(Subscription), (int)HttpStatusCode.OK)]
    [ProducesErrorResponseType(typeof(Neuroglia.ProblemDetails))]
    public virtual async Task<IActionResult> ReplaySubscription(string name, CancellationToken cancellationToken = default)
    {
        return this.Process(await this.Mediator.ExecuteAsync(new ReplaySubscriptionCommand(name), cancellationToken).ConfigureAwait(false));
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
        var result = await this.Mediator.ExecuteAsync(new ExportSubscriptionCommand(name), cancellationToken).ConfigureAwait(false);
        if (result.Status is < 200 or >= 300 || result.Data == null) return this.Process(result);
        return this.File(result.Data, "application/x-yaml", $"{name}.yaml");
    }

}