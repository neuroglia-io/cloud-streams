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

using CloudStreams.Core.Application.Services;

namespace CloudStreams.Core.Api.Controllers;

/// <summary>
/// Represents the API controller that implements the CloudEvents Subscriptions API, on top of Cloud Streams subscriptions (see #219)
/// </summary>
/// <param name="manager">The service used to manage subscriptions through the CloudEvents Subscriptions API</param>
/// <remarks>
/// The HTTP binding of the specification is yet to be defined: subscriptions are created with <c>POST</c>, which answers <c>201 Created</c> with their location,
/// listed and retrieved with <c>GET</c>, updated with <c>PUT</c>, which does not create missing subscriptions, and deleted with <c>DELETE</c>, which answers the
/// deleted subscription. Errors are described with problem details. Reading requires the reader role, and writing the admin role
/// </remarks>
[ApiController]
[Route("api/cloudevents/v1/subscriptions")]
[Route("api/cloudevents/v1/namespaces/{namespace}/subscriptions")]
public class CloudEventSubscriptionsController(CloudEventSubscriptionManager manager)
    : ControllerBase
{

    /// <summary>
    /// Lists the subscriptions
    /// </summary>
    /// <param name="namespace">The namespace the subscriptions belong to. The default namespace when the route names none</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/> whose body is the subscriptions</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<CloudEventSubscription>), (int)HttpStatusCode.OK)]
    public virtual async Task<IActionResult> ListSubscriptions(string? @namespace = null, CancellationToken cancellationToken = default) => this.Ok(await manager.ListAsync(@namespace, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// Gets the specified subscription
    /// </summary>
    /// <param name="namespace">The namespace the subscriptions belong to. The default namespace when the route names none</param>
    /// <param name="id">The id of the subscription to get</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/> whose body is the subscription</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(CloudEventSubscription), (int)HttpStatusCode.OK)]
    [ProducesErrorResponseType(typeof(Neuroglia.ProblemDetails))]
    public virtual async Task<IActionResult> GetSubscription(string? @namespace, string id, CancellationToken cancellationToken = default)
    {
        var subscription = await manager.GetAsync(id, @namespace, cancellationToken).ConfigureAwait(false);
        return subscription == null ? NotFound(id) : this.Ok(subscription);
    }

    /// <summary>
    /// Creates a new subscription. Its id, if any, is ignored: the subscription manager assigns one
    /// </summary>
    /// <param name="namespace">The namespace the subscriptions belong to. The default namespace when the route names none</param>
    /// <param name="subscription">The subscription to create</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/> whose body is the created subscription</returns>
    [HttpPost]
    [ProducesResponseType(typeof(CloudEventSubscription), (int)HttpStatusCode.Created)]
    [ProducesErrorResponseType(typeof(Neuroglia.ProblemDetails))]
    public virtual async Task<IActionResult> CreateSubscription(string? @namespace, [FromBody] CloudEventSubscription subscription, CancellationToken cancellationToken = default)
    {
        var created = await manager.CreateAsync(subscription, @namespace, cancellationToken).ConfigureAwait(false);
        var scope = string.IsNullOrWhiteSpace(@namespace) ? string.Empty : $"namespaces/{Uri.EscapeDataString(@namespace)}/";
        return this.Created($"{this.Request.PathBase}/api/cloudevents/v1/{scope}subscriptions/{Uri.EscapeDataString(created.Id!)}", created);
    }

    /// <summary>
    /// Updates the specified subscription
    /// </summary>
    /// <param name="namespace">The namespace the subscriptions belong to. The default namespace when the route names none</param>
    /// <param name="id">The id of the subscription to update</param>
    /// <param name="subscription">The subscription to update it with</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/> whose body is the updated subscription</returns>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(CloudEventSubscription), (int)HttpStatusCode.OK)]
    [ProducesErrorResponseType(typeof(Neuroglia.ProblemDetails))]
    public virtual async Task<IActionResult> UpdateSubscription(string? @namespace, string id, [FromBody] CloudEventSubscription subscription, CancellationToken cancellationToken = default)
    {
        var updated = await manager.UpdateAsync(id, subscription, @namespace, cancellationToken).ConfigureAwait(false);
        return updated == null ? NotFound(id) : this.Ok(updated);
    }

    /// <summary>
    /// Deletes the specified subscription
    /// </summary>
    /// <param name="namespace">The namespace the subscriptions belong to. The default namespace when the route names none</param>
    /// <param name="id">The id of the subscription to delete</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/> whose body is the deleted subscription</returns>
    [HttpDelete("{id}")]
    [ProducesResponseType(typeof(CloudEventSubscription), (int)HttpStatusCode.OK)]
    [ProducesErrorResponseType(typeof(Neuroglia.ProblemDetails))]
    public virtual async Task<IActionResult> DeleteSubscription(string? @namespace, string id, CancellationToken cancellationToken = default)
    {
        var deleted = await manager.DeleteAsync(id, @namespace, cancellationToken).ConfigureAwait(false);
        return deleted == null ? NotFound(id) : this.Ok(deleted);
    }

    static ObjectResult NotFound(string id) => new(new Neuroglia.ProblemDetails(ProblemTypes.NotFound, "Not Found", (int)HttpStatusCode.NotFound, $"The subscription '{id}' does not exist"))
    {
        StatusCode = (int)HttpStatusCode.NotFound,
        ContentTypes = { "application/problem+json" }
    };

}
