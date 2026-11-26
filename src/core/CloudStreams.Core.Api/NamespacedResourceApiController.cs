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
using CloudStreams.Core.Application.Commands.Resources.Generic;
using CloudStreams.Core.Application.Queries.Resources.Generic;

namespace CloudStreams.Core.Api;

/// <summary>
/// Represents a <see cref="ResourceApiController{TResource}"/> used to manage namespaced resources (see #215)
/// </summary>
/// <typeparam name="TResource">The type of namespaced <see cref="IResource"/> to manage</typeparam>
/// <remarks>
/// Implementations are routed at <c>api/resources/v1/namespaces/{namespace}/{plural}</c>. The resources of all the namespaces the user may access are
/// also served at <c>api/resources/v1/{plural}</c>
/// </remarks>
/// <inheritdoc/>
[NamespacedResourceRouteConvention]
public abstract class NamespacedResourceApiController<TResource>(IMediator mediator)
    : ResourceApiController<TResource>(mediator)
    where TResource : class, IResource, new()
{

    /// <summary>
    /// Gets the token replaced, in the routes of namespaced resource controllers, by the plural name of the resources they manage
    /// </summary>
    public const string ResourcesToken = "[resources]";

    /// <summary>
    /// Creates a new resource in the specified namespace
    /// </summary>
    /// <param name="namespace">The namespace to create the resource in</param>
    /// <param name="resource">The resource to create</param>
    /// <param name="dryRun">A boolean indicating whether or not to persist changes</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/></returns>
    [HttpPost]
    [ProducesResponseType(typeof(Resource), (int)HttpStatusCode.Created)]
    [ProducesErrorResponseType(typeof(Neuroglia.ProblemDetails))]
    public virtual async Task<IActionResult> CreateResource(string @namespace, [FromBody] TResource resource, bool dryRun = false, CancellationToken cancellationToken = default)
    {
        if (!this.ModelState.IsValid) return this.ValidationProblem(this.ModelState);
        if (!this.TryBindNamespace(@namespace, resource, out var problem)) return problem;
        return this.Process(await this.Mediator.ExecuteAsync(new CreateResourceCommand<TResource>(resource, dryRun), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Gets the definition of the managed resources
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/></returns>
    [HttpGet("~/api/resources/v1/" + ResourcesToken + "/definition")]
    [NamespaceFiltered]
    [ProducesResponseType(typeof(ResourceDefinition), (int)HttpStatusCode.OK)]
    [ProducesErrorResponseType(typeof(Neuroglia.ProblemDetails))]
    public virtual async Task<IActionResult> GetResourceDefinition(CancellationToken cancellationToken = default)
    {
        return this.Process(await this.Mediator.ExecuteAsync(new GetResourceDefinitionQuery<TResource>(), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Gets the resources of all the namespaces the user may read
    /// </summary>
    /// <param name="labelSelector">A comma-separated list of label selectors, if any</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/></returns>
    [HttpGet("~/api/resources/v1/" + ResourcesToken)]
    [NamespaceFiltered]
    [ProducesResponseType(typeof(IAsyncEnumerable<Resource>), (int)HttpStatusCode.OK)]
    [ProducesErrorResponseType(typeof(Neuroglia.ProblemDetails))]
    public virtual async Task<IActionResult> GetAllResources(string? labelSelector = null, CancellationToken cancellationToken = default)
    {
        if (!this.TryParseLabelSelectors(labelSelector, out var labelSelectors)) return this.InvalidLabelSelector(labelSelector!);
        var response = await this.Mediator.ExecuteAsync(new GetResourcesQuery<TResource>(null, labelSelectors), cancellationToken).ConfigureAwait(false);
        if (response.Status is < 200 or >= 300 || response.Data == null) return this.Process(response);
        var namespaces = this.GetAccessibleNamespaces(CloudStreamsAccessLevel.Read);
        return this.Ok(namespaces == null ? response.Data : response.Data.Where(r => IsIn(r, namespaces)));
    }

    /// <summary>
    /// Lists the resources of all the namespaces the user may read
    /// </summary>
    /// <param name="labelSelector">A comma-separated list of label selectors, if any</param>
    /// <param name="maxResults">The maximum amount of resources to list</param>
    /// <param name="continuationToken">The token used to continue a previous listing, if any</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/></returns>
    /// <remarks>Pages hold the resources the user may read among the requested amount of resources, and may therefore be shorter than requested</remarks>
    [HttpGet("~/api/resources/v1/" + ResourcesToken + "/list")]
    [NamespaceFiltered]
    [ProducesResponseType(typeof(Collection<Resource>), (int)HttpStatusCode.OK)]
    [ProducesErrorResponseType(typeof(Neuroglia.ProblemDetails))]
    public virtual async Task<IActionResult> ListAllResources(string? labelSelector = null, ulong? maxResults = null, string? continuationToken = null, CancellationToken cancellationToken = default)
    {
        if (!this.TryParseLabelSelectors(labelSelector, out var labelSelectors)) return this.InvalidLabelSelector(labelSelector!);
        var response = await this.Mediator.ExecuteAsync(new ListResourcesQuery<TResource>(null, labelSelectors, maxResults, continuationToken), cancellationToken).ConfigureAwait(false);
        if (response.Status is < 200 or >= 300 || response.Data == null) return this.Process(response);
        var namespaces = this.GetAccessibleNamespaces(CloudStreamsAccessLevel.Read);
        if (namespaces == null) return this.Process(response);
        return this.Ok(new Collection<TResource>(response.Data.Metadata, response.Data.Items?.Where(r => IsIn(r, namespaces)).ToList() ?? []));
    }

    /// <summary>
    /// Watches the resources of all the namespaces the user may read
    /// </summary>
    /// <param name="labelSelector">A comma-separated list of label selectors, if any</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/></returns>
    [HttpGet("~/api/resources/v1/" + ResourcesToken + "/watch")]
    [NamespaceFiltered]
    [ProducesResponseType(typeof(IAsyncEnumerable<Resource>), (int)HttpStatusCode.OK)]
    [ProducesErrorResponseType(typeof(Neuroglia.ProblemDetails))]
    public virtual async Task<IActionResult> WatchAllResources(string? labelSelector = null, CancellationToken cancellationToken = default)
    {
        if (!this.TryParseLabelSelectors(labelSelector, out var labelSelectors)) return this.InvalidLabelSelector(labelSelector!);
        var response = await this.Mediator.ExecuteAsync(new WatchResourcesQuery<TResource>(null, labelSelectors), cancellationToken).ConfigureAwait(false);
        if (response.Status is < 200 or >= 300 || response.Data == null) return this.Process(response);
        var namespaces = this.GetAccessibleNamespaces(CloudStreamsAccessLevel.Read);
        var events = response.Data.ToAsyncEnumerable();
        return this.Ok(namespaces == null ? events : events.Where(e => IsIn(e.Resource, namespaces)));
    }

    /// <summary>
    /// Gets the resources of the specified namespace
    /// </summary>
    /// <param name="namespace">The namespace the resources to get belong to</param>
    /// <param name="labelSelector">A comma-separated list of label selectors, if any</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/></returns>
    [HttpGet]
    [ProducesResponseType(typeof(IAsyncEnumerable<Resource>), (int)HttpStatusCode.OK)]
    [ProducesErrorResponseType(typeof(Neuroglia.ProblemDetails))]
    public virtual async Task<IActionResult> GetResources(string @namespace, string? labelSelector = null, CancellationToken cancellationToken = default)
    {
        if (!this.TryParseLabelSelectors(labelSelector, out var labelSelectors)) return this.InvalidLabelSelector(labelSelector!);
        return this.Process(await this.Mediator.ExecuteAsync(new GetResourcesQuery<TResource>(@namespace, labelSelectors), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Lists the resources of the specified namespace
    /// </summary>
    /// <param name="namespace">The namespace the resources to list belong to</param>
    /// <param name="labelSelector">A comma-separated list of label selectors, if any</param>
    /// <param name="maxResults">The maximum amount of resources to list</param>
    /// <param name="continuationToken">The token used to continue a previous listing, if any</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/></returns>
    [HttpGet("list")]
    [ProducesResponseType(typeof(Collection<Resource>), (int)HttpStatusCode.OK)]
    [ProducesErrorResponseType(typeof(Neuroglia.ProblemDetails))]
    public virtual async Task<IActionResult> ListResources(string @namespace, string? labelSelector = null, ulong? maxResults = null, string? continuationToken = null, CancellationToken cancellationToken = default)
    {
        if (!this.TryParseLabelSelectors(labelSelector, out var labelSelectors)) return this.InvalidLabelSelector(labelSelector!);
        return this.Process(await this.Mediator.ExecuteAsync(new ListResourcesQuery<TResource>(@namespace, labelSelectors, maxResults, continuationToken), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Watches the resources of the specified namespace
    /// </summary>
    /// <param name="namespace">The namespace the resources to watch belong to</param>
    /// <param name="labelSelector">A comma-separated list of label selectors, if any</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/></returns>
    [HttpGet("watch")]
    [ProducesResponseType(typeof(IAsyncEnumerable<Resource>), (int)HttpStatusCode.OK)]
    [ProducesErrorResponseType(typeof(Neuroglia.ProblemDetails))]
    public virtual async Task<IActionResult> WatchResources(string @namespace, string? labelSelector = null, CancellationToken cancellationToken = default)
    {
        if (!this.TryParseLabelSelectors(labelSelector, out var labelSelectors)) return this.InvalidLabelSelector(labelSelector!);
        var response = await this.Mediator.ExecuteAsync(new WatchResourcesQuery<TResource>(@namespace, labelSelectors), cancellationToken).ConfigureAwait(false);
        if (response.Status is < 200 or >= 300 || response.Data == null) return this.Process(response);
        return this.Ok(response.Data.ToAsyncEnumerable());
    }

    /// <summary>
    /// Gets the specified resource
    /// </summary>
    /// <param name="namespace">The namespace the resource to get belongs to</param>
    /// <param name="name">The name of the resource to get</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/></returns>
    [HttpGet("{name}")]
    [ProducesResponseType(typeof(Resource), (int)HttpStatusCode.OK)]
    [ProducesErrorResponseType(typeof(Neuroglia.ProblemDetails))]
    public virtual async Task<IActionResult> GetResource(string @namespace, string name, CancellationToken cancellationToken = default)
    {
        return this.Process(await this.Mediator.ExecuteAsync(new GetResourceQuery<TResource>(name, @namespace), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Replaces the specified resource
    /// </summary>
    /// <param name="namespace">The namespace the resource to replace belongs to</param>
    /// <param name="resource">The resource to replace</param>
    /// <param name="dryRun">A boolean indicating whether or not to persist changes</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/></returns>
    [HttpPut]
    [ProducesResponseType(typeof(Resource), (int)HttpStatusCode.OK)]
    [ProducesErrorResponseType(typeof(Neuroglia.ProblemDetails))]
    public virtual async Task<IActionResult> ReplaceResource(string @namespace, [FromBody] TResource resource, bool dryRun = false, CancellationToken cancellationToken = default)
    {
        if (!this.ModelState.IsValid) return this.ValidationProblem(this.ModelState);
        if (!this.TryBindNamespace(@namespace, resource, out var problem)) return problem;
        return this.Process(await this.Mediator.ExecuteAsync(new ReplaceResourceCommand<TResource>(resource, dryRun), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Patches the specified resource
    /// </summary>
    /// <param name="namespace">The namespace the resource to patch belongs to</param>
    /// <param name="name">The name of the resource to patch</param>
    /// <param name="patch">The patch to apply</param>
    /// <param name="dryRun">A boolean indicating whether or not to persist changes</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/></returns>
    [HttpPatch("{name}")]
    [ProducesResponseType(typeof(Resource), (int)HttpStatusCode.OK)]
    [ProducesErrorResponseType(typeof(Neuroglia.ProblemDetails))]
    public virtual async Task<IActionResult> PatchResource(string @namespace, string name, [FromBody] Patch patch, bool dryRun = false, CancellationToken cancellationToken = default)
    {
        if (!this.ModelState.IsValid) return this.ValidationProblem(this.ModelState);
        return this.Process(await this.Mediator.ExecuteAsync(new PatchResourceCommand<TResource>(name, @namespace, patch, dryRun), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Patches the status of the specified resource
    /// </summary>
    /// <param name="namespace">The namespace the resource to patch belongs to</param>
    /// <param name="name">The name of the resource to patch</param>
    /// <param name="patch">The patch to apply</param>
    /// <param name="dryRun">A boolean indicating whether or not to persist changes</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/></returns>
    [HttpPatch("{name}/status")]
    [ProducesResponseType(typeof(Resource), (int)HttpStatusCode.OK)]
    [ProducesErrorResponseType(typeof(Neuroglia.ProblemDetails))]
    public virtual async Task<IActionResult> PatchResourceStatus(string @namespace, string name, [FromBody] Patch patch, bool dryRun = false, CancellationToken cancellationToken = default)
    {
        if (!this.ModelState.IsValid) return this.ValidationProblem(this.ModelState);
        return this.Process(await this.Mediator.ExecuteAsync(new PatchResourceStatusCommand<TResource>(name, @namespace, patch, dryRun), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Deletes the specified resource
    /// </summary>
    /// <param name="namespace">The namespace the resource to delete belongs to</param>
    /// <param name="name">The name of the resource to delete</param>
    /// <param name="dryRun">A boolean indicating whether or not to persist changes</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/></returns>
    [HttpDelete("{name}")]
    [ProducesResponseType(typeof(Resource), (int)HttpStatusCode.OK)]
    [ProducesErrorResponseType(typeof(Neuroglia.ProblemDetails))]
    public virtual async Task<IActionResult> DeleteResource(string @namespace, string name, bool dryRun = false, CancellationToken cancellationToken = default)
    {
        if (!this.ModelState.IsValid) return this.ValidationProblem(this.ModelState);
        return this.Process(await this.Mediator.ExecuteAsync(new DeleteResourceCommand<TResource>(name, @namespace, dryRun), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Binds the specified resource to the namespace of the route, refusing a resource that declares another namespace
    /// </summary>
    /// <param name="namespace">The namespace of the route</param>
    /// <param name="resource">The resource to bind</param>
    /// <param name="problem">An <see cref="IActionResult"/> that describes why the resource could not be bound, if it could not</param>
    /// <returns>A boolean indicating whether or not the resource could be bound to the namespace of the route</returns>
    protected virtual bool TryBindNamespace(string @namespace, TResource resource, out IActionResult problem)
    {
        ArgumentNullException.ThrowIfNull(resource);
        problem = null!;
        if (string.IsNullOrWhiteSpace(resource.Metadata.Namespace))
        {
            resource.Metadata.Namespace = @namespace;
            return true;
        }
        if (resource.Metadata.Namespace == @namespace) return true;
        this.ModelState.AddModelError("metadata.namespace", $"The namespace of the resource ('{resource.Metadata.Namespace}') differs from the namespace of the route ('{@namespace}')");
        problem = this.ValidationProblem("Bad Request", statusCode: (int)HttpStatusCode.BadRequest, title: "Bad Request", modelStateDictionary: this.ModelState);
        return false;
    }

    /// <summary>
    /// Determines whether or not the specified resource belongs to one of the specified namespaces
    /// </summary>
    /// <param name="resource">The resource to check</param>
    /// <param name="namespaces">The namespaces to check</param>
    /// <returns>A boolean indicating whether or not the specified resource belongs to one of the specified namespaces</returns>
    static bool IsIn(IResource resource, IReadOnlySet<string> namespaces) => namespaces.Contains(resource.GetNamespace() ?? Namespace.DefaultNamespaceName);

}
