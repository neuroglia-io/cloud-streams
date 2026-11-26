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
using CloudStreams.Core.Application.Queries.Resources.Generic;
using CloudStreams.Core.Application.Validation;

namespace CloudStreams.Core.Api.Controllers;

/// <summary>
/// Represents the <see cref="ClusterResourceApiController{TResource}"/> used to manage <see cref="Namespace"/>s, which isolate tenants from each other (see #215)
/// </summary>
/// <param name="mediator">The service used to mediate calls</param>
/// <param name="repository">The service used to manage resources</param>
[Route("api/resources/v1/namespaces")]
public class NamespacesController(IMediator mediator, IResourceRepository repository)
    : ClusterResourceApiController<Namespace>(mediator)
{

    /// <summary>
    /// Gets the namespaces the user may read
    /// </summary>
    /// <param name="labelSelector">A comma-separated list of label selectors, if any</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/></returns>
    /// <remarks>Users granted access to some namespaces only are answered with these namespaces, so that the dashboard can offer them</remarks>
    [HttpGet]
    [NamespaceFiltered]
    [ProducesResponseType(typeof(IAsyncEnumerable<Namespace>), (int)HttpStatusCode.OK)]
    [ProducesErrorResponseType(typeof(Neuroglia.ProblemDetails))]
    public override async Task<IActionResult> GetResources(string? labelSelector = null, CancellationToken cancellationToken = default)
    {
        if (!this.TryParseLabelSelectors(labelSelector, out var labelSelectors)) return this.InvalidLabelSelector(labelSelector!);
        var response = await this.Mediator.ExecuteAsync(new GetResourcesQuery<Namespace>(null, labelSelectors), cancellationToken).ConfigureAwait(false);
        if (response.Status is < 200 or >= 300 || response.Data == null) return this.Process(response);
        var namespaces = this.GetAccessibleNamespaces(CloudStreamsAccessLevel.Read);
        return this.Ok(namespaces == null ? response.Data : response.Data.Where(n => namespaces.Contains(n.GetName())));
    }

    /// <summary>
    /// Creates a new namespace, whose name must be a DNS label
    /// </summary>
    /// <param name="resource">The namespace to create</param>
    /// <param name="dryRun">A boolean indicating whether or not to persist changes</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/></returns>
    /// <remarks>The name of a namespace is part of the names of streams, keys, secret paths, environment variables and roles</remarks>
    [HttpPost]
    [ProducesResponseType(typeof(Namespace), (int)HttpStatusCode.Created)]
    [ProducesErrorResponseType(typeof(Neuroglia.ProblemDetails))]
    public override async Task<IActionResult> CreateResource([FromBody] Namespace resource, bool dryRun = false, CancellationToken cancellationToken = default)
    {
        if (resource != null && !CloudStreamsResourceValidator.IsValidNamespaceName(resource.GetName()))
        {
            this.ModelState.AddModelError("metadata.name", $"'{resource.GetName()}' is not a valid namespace name: it must be at most 63 lowercase letters, digits and dashes, and start and end with a letter or a digit");
            return this.ValidationProblem("Bad Request", statusCode: (int)HttpStatusCode.BadRequest, title: "Bad Request", modelStateDictionary: this.ModelState);
        }
        return await base.CreateResource(resource!, dryRun, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes the specified namespace, which must hold no resource
    /// </summary>
    /// <param name="name">The name of the namespace to delete</param>
    /// <param name="dryRun">A boolean indicating whether or not to persist changes</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/></returns>
    /// <remarks>
    /// Deleting a namespace does not delete its resources, which would be left behind, out of reach: a namespace that still holds resources is refused, as is the
    /// default namespace, which resources created without a namespace belong to
    /// </remarks>
    [HttpDelete("{name}")]
    [ProducesResponseType(typeof(Namespace), (int)HttpStatusCode.OK)]
    [ProducesErrorResponseType(typeof(Neuroglia.ProblemDetails))]
    public override async Task<IActionResult> DeleteResource(string name, bool dryRun = false, CancellationToken cancellationToken = default)
    {
        if (name == Namespace.DefaultNamespaceName) return Conflict($"The namespace '{name}' cannot be deleted");
        var kinds = new List<string>();
        if (await this.HoldsAnyAsync<Subscription>(name, cancellationToken).ConfigureAwait(false)) kinds.Add("subscriptions");
        if (await this.HoldsAnyAsync<Gateway>(name, cancellationToken).ConfigureAwait(false)) kinds.Add("gateways");
        if (await this.HoldsAnyAsync<Broker>(name, cancellationToken).ConfigureAwait(false)) kinds.Add("brokers");
        if (await this.HoldsAnyAsync<Sink>(name, cancellationToken).ConfigureAwait(false)) kinds.Add("sinks");
        if (await this.HoldsAnyAsync<Feed>(name, cancellationToken).ConfigureAwait(false)) kinds.Add("feeds");
        if (kinds.Count > 0) return Conflict($"The namespace '{name}' still holds {string.Join(", ", kinds)}: delete them first");
        return await base.DeleteResource(name, dryRun, cancellationToken).ConfigureAwait(false);
    }

    async Task<bool> HoldsAnyAsync<TResource>(string @namespace, CancellationToken cancellationToken)
        where TResource : class, IResource, new()
    {
        await foreach (var _ in repository.GetAllAsync<TResource>(@namespace, null, cancellationToken).ConfigureAwait(false)) return true;
        return false;
    }

    static ObjectResult Conflict(string detail) => new(new Neuroglia.ProblemDetails(new Uri("https://cloud-streams.io/problems/namespace-not-empty"), "Conflict", (int)HttpStatusCode.Conflict, detail))
    {
        StatusCode = (int)HttpStatusCode.Conflict,
        ContentTypes = { "application/problem+json" }
    };

}
