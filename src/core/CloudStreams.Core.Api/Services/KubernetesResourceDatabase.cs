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
using k8s;
using k8s.Autorest;
using Json.Patch;
using k8s.Models;
using Microsoft.Extensions.Logging;
using Neuroglia.Data.PatchModel.Services;
using System.Reactive.Disposables;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CloudStreams.Core.Api.Services;

/// <summary>
/// Represents the <see cref="IDatabase"/> that stores the resources of Cloud Streams as Kubernetes custom resources (see #275)
/// </summary>
/// <remarks>
/// <para>It extends the <see cref="KubernetesDatabase"/> of Neuroglia, whose operations it all overrides, until its fixes are released (see #275):</para>
/// <list type="bullet">
/// <item><description>patches and replacements are applied with optimistic concurrency, as <see cref="AtomicRedisDatabase"/> does;</description></item>
/// <item><description>the <c>status</c> sub-resource is supported;</description></item>
/// <item><description>watches resume where they stopped when the API server closes them, which it does every few minutes, and apply label selectors;</description></item>
/// <item><description>lists are paged;</description></item>
/// <item><description>errors of the API server are reported as <see cref="ProblemDetailsException"/>s, with the statuses the Redis database reports.</description></item>
/// </list>
/// <para>
/// Resource definitions are the CustomResourceDefinitions the Helm chart installs: the definitions of Cloud Streams, whose schemas are richer, are returned for those
/// that are installed. Namespaces are the Kubernetes namespaces labelled with <see cref="KubernetesResourceStoreOptions.NamespaceLabel"/>, and the default namespace
/// of Cloud Streams is <see cref="KubernetesResourceStoreOptions.DefaultNamespace"/> (see #277)
/// </para>
/// <para>
/// A process restricted to some namespaces by <see cref="KubernetesResourceStoreOptions.Namespaces"/> refuses the resources of the others, and lists and watches
/// those it serves one namespace at a time, so that it only needs rights in their Kubernetes namespaces
/// </para>
/// </remarks>
/// <param name="logger">The service used to perform logging</param>
/// <param name="kubernetes">The service used to interact with the Kubernetes API</param>
/// <param name="serializer">The service used to serialize/deserialize data to/from JSON</param>
/// <param name="patchHandlers">The services used to apply patches</param>
/// <param name="options">The options of the Kubernetes store</param>
public class KubernetesResourceDatabase(ILogger<KubernetesDatabase> logger, IKubernetes kubernetes, IJsonSerializer serializer, IEnumerable<IPatchHandler> patchHandlers, KubernetesResourceStoreOptions options)
    : KubernetesDatabase(logger, kubernetes, serializer)
{

    /// <summary>
    /// Gets the maximum amount of attempts to update a resource that keeps changing concurrently, when no version is expected
    /// </summary>
    public const int MaxUpdateAttempts = 16;

    /// <summary>
    /// Gets the dry-run value of the Kubernetes API that only validates a request
    /// </summary>
    const string DryRunAll = "All";

    /// <summary>
    /// Gets the options of the Kubernetes store
    /// </summary>
    protected KubernetesResourceStoreOptions Options { get; } = options;

    /// <summary>
    /// Gets the services used to apply patches
    /// </summary>
    protected IEnumerable<IPatchHandler> PatchHandlers { get; } = patchHandlers;

    #region Routing

    static bool IsDefinition(string group, string plural) => group == ResourceDefinition.ResourceGroup && plural == ResourceDefinition.ResourcePlural;

    static bool IsNamespace(string group, string plural) => group == NamespaceDefinition.ResourceGroup && plural == NamespaceDefinition.ResourcePlural;

    /// <summary>
    /// Gets the Kubernetes namespace of the specified namespace of Cloud Streams
    /// </summary>
    /// <param name="namespace">The namespace of Cloud Streams</param>
    /// <returns>The name of the Kubernetes namespace, if any</returns>
    public virtual string? ToKubernetesNamespace(string? @namespace) => string.IsNullOrWhiteSpace(@namespace) ? null : @namespace == Namespace.DefaultNamespaceName ? this.Options.DefaultNamespace : @namespace;

    /// <summary>
    /// Gets the namespace of Cloud Streams of the specified Kubernetes namespace
    /// </summary>
    /// <param name="namespace">The name of the Kubernetes namespace</param>
    /// <returns>The namespace of Cloud Streams, if any</returns>
    public virtual string? FromKubernetesNamespace(string? @namespace) => string.IsNullOrWhiteSpace(@namespace) ? null : @namespace == this.Options.DefaultNamespace ? Namespace.DefaultNamespaceName : @namespace;

    IReadOnlyList<string>? _servedNamespaces;
    bool _servedNamespacesResolved;

    /// <summary>
    /// Gets the namespaces of Cloud Streams the process serves, if not all of them, as <see cref="KubernetesResourceStoreOptions.Namespaces"/> configures them
    /// </summary>
    /// <remarks>The Kubernetes namespace of the default namespace may be listed by either of its names</remarks>
    public virtual IReadOnlyList<string>? ServedNamespaces
    {
        get
        {
            if (this._servedNamespacesResolved) return this._servedNamespaces;
            var served = this.Options.Namespaces?.Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => this.FromKubernetesNamespace(n.Trim())!).Distinct(StringComparer.Ordinal).ToList();
            this._servedNamespaces = served?.Count > 0 ? served : null;
            this._servedNamespacesResolved = true;
            return this._servedNamespaces;
        }
    }

    /// <summary>
    /// Determines whether the process serves the specified namespace of Cloud Streams
    /// </summary>
    /// <param name="namespace">The namespace of Cloud Streams to check</param>
    /// <returns>A boolean indicating whether the process serves the specified namespace</returns>
    public virtual bool IsServed(string? @namespace) => this.ServedNamespaces == null || string.IsNullOrWhiteSpace(@namespace) || this.ServedNamespaces.Contains(@namespace);

    /// <summary>
    /// Determines whether the resources of the specified kind are scoped by the cluster rather than by a namespace
    /// </summary>
    static bool IsClusterScoped(string group, string plural) => IsDefinition(group, plural) || IsNamespace(group, plural)
        || CloudStreamsDefaults.Resources.Definitions.AsEnumerable().Any(d => d.Spec.Group == group && d.Spec.Names.Plural == plural && d.Spec.Scope == ResourceScope.Cluster);

    /// <summary>
    /// Ensures that the process serves the namespace of the specified operation: that of the resource, or the namespace itself for namespaces
    /// </summary>
    void EnsureServed(string group, string plural, string? name, string? @namespace)
    {
        if (this.ServedNamespaces == null || IsDefinition(group, plural)) return;
        var served = IsNamespace(group, plural) ? name : @namespace;
        if (!this.IsServed(served)) throw new ProblemDetailsException(this.NamespaceNotServed(served!));
    }

    Neuroglia.ProblemDetails NamespaceNotServed(string @namespace) => new(new Uri("https://cloud-streams.io/problems/namespace-not-served"), "Forbidden", (int)HttpStatusCode.Forbidden,
        $"The namespace '{@namespace}' is not served by this process, which only serves the namespaces of '{ResourceStoreOptions.ConfigurationSection}:Kubernetes:Namespaces': {string.Join(", ", this.ServedNamespaces!.Select(n => $"'{n}'"))}");

    /// <summary>
    /// Determines whether a list or a watch of the specified resources spans the served namespaces, one at a time, rather than the whole cluster
    /// </summary>
    bool SpansServedNamespaces(string group, string plural, string? @namespace) => this.ServedNamespaces != null && string.IsNullOrWhiteSpace(@namespace) && !IsClusterScoped(group, plural);

    #endregion

    #region IDatabase

    /// <inheritdoc/>
    /// <remarks>
    /// Checks that the API server can be reached, by reading the first served namespace if the process only serves some, since it may not list the others. The
    /// CustomResourceDefinitions are installed by the Helm chart, never by the application (see #276)
    /// </remarks>
    public override async Task<bool> InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (this.ServedNamespaces == null) await this.RunAsync(() => this.Kubernetes.CoreV1.ListNamespaceAsync(limit: 1, cancellationToken: cancellationToken), null).ConfigureAwait(false);
        else await this.ReadKubernetesNamespaceAsync(this.ToKubernetesNamespace(this.ServedNamespaces[0])!, cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc/>
    public override async Task<IResource> CreateResourceAsync(IResource resource, string group, string version, string plural, string? @namespace = null, bool dryRun = false, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(resource);
        if (IsDefinition(group, plural)) throw new ProblemDetailsException(DefinitionsAreReadOnly(resource.GetName()));
        if (resource.Metadata.Name?.Trim().EndsWith('-') == true) resource.Metadata.Name = $"{resource.Metadata.Name}{Guid.NewGuid().ToString("N")[..15].ToLowerInvariant()}";
        var reference = new ResourceReference(new(group, version, plural), resource.GetName(), @namespace ?? resource.GetNamespace());
        this.EnsureServed(group, plural, reference.Name, reference.Namespace);
        if (IsNamespace(group, plural)) return await this.CreateNamespaceAsync(resource, reference, dryRun, cancellationToken).ConfigureAwait(false);
        var body = this.ToKubernetes(resource, group, version);
        var kubernetesNamespace = this.ToKubernetesNamespace(reference.Namespace);
        var result = await this.RunAsync(() => kubernetesNamespace == null
            ? this.Kubernetes.CustomObjects.CreateClusterCustomObjectAsync(body, group, version, plural, dryRun ? DryRunAll : null, cancellationToken: cancellationToken)
            : this.Kubernetes.CustomObjects.CreateNamespacedCustomObjectAsync(body, group, version, kubernetesNamespace, plural, dryRun ? DryRunAll : null, cancellationToken: cancellationToken), reference).ConfigureAwait(false);
        return this.ToResource(result);
    }

    /// <inheritdoc/>
    public override async Task<IResource?> GetResourceAsync(string group, string version, string plural, string name, string? @namespace = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (IsDefinition(group, plural)) return await this.GetDefinitionAsync(name, cancellationToken).ConfigureAwait(false);
        // a namespace the process does not serve does not exist for it
        if (IsNamespace(group, plural)) return this.IsServed(name) ? await this.GetNamespaceAsync(name, cancellationToken).ConfigureAwait(false) : null;
        this.EnsureServed(group, plural, name, @namespace);
        var node = await this.GetCustomObjectAsync(group, version, plural, name, @namespace, cancellationToken).ConfigureAwait(false);
        return node == null ? null : this.ToResource(node);
    }

    /// <inheritdoc/>
    public override async Task<ICollection> ListResourcesAsync(string group, string version, string plural, string? @namespace = null, IEnumerable<LabelSelector>? labelSelectors = null, ulong? maxResults = null, string? continuationToken = null, CancellationToken cancellationToken = default)
    {
        var limit = maxResults.HasValue ? (int)Math.Min(maxResults.Value, int.MaxValue) : (int?)null;
        var page = await this.ListPageAsync(group, version, plural, @namespace, labelSelectors, limit, continuationToken, cancellationToken).ConfigureAwait(false);
        var kind = page.Items.FirstOrDefault()?.Kind ?? await this.GetKindAsync(group, plural, cancellationToken).ConfigureAwait(false);
        return new Collection(Resource.BuildApiVersion(group, version), kind, new CollectionMetadata() { Continue = page.Continue, ResourceVersion = page.ResourceVersion! }, page.Items);
    }

    /// <inheritdoc/>
    public override async IAsyncEnumerable<IResource> GetResourcesAsync(string group, string version, string plural, string? @namespace = null, IEnumerable<LabelSelector>? labelSelectors = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        string? continuationToken = null;
        do
        {
            var page = await this.ListPageAsync(group, version, plural, @namespace, labelSelectors, this.Options.PageSize, continuationToken, cancellationToken).ConfigureAwait(false);
            foreach (var item in page.Items) yield return item;
            continuationToken = page.Continue;
        }
        while (!string.IsNullOrWhiteSpace(continuationToken));
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The watch starts with the current version of the resources, so that it only emits the changes made afterwards, as the Redis database does. It resumes where it
    /// stopped when the API server closes it, and starts over from the current version if that version expired. A process that only serves some namespaces watches
    /// each of them, and merges their changes
    /// </remarks>
    public override async Task<IResourceWatch> WatchResourcesAsync(string group, string version, string plural, string? @namespace = null, IEnumerable<LabelSelector>? labelSelectors = null, CancellationToken cancellationToken = default)
    {
        var selectors = labelSelectors?.ToList();
        if (!IsNamespace(group, plural)) this.EnsureServed(group, plural, null, @namespace);
        // the namespaces watched one at a time, with the version each starts from
        var watched = new List<(string? Namespace, string? ResourceVersion)>();
        foreach (var watchedNamespace in this.SpansServedNamespaces(group, plural, @namespace) ? this.ServedNamespaces!.Cast<string?>() : [@namespace])
        {
            watched.Add((watchedNamespace, await this.GetCurrentVersionAsync(group, version, plural, watchedNamespace, selectors, cancellationToken).ConfigureAwait(false)));
        }
        var observable = Observable.Create<IResourceWatchEvent>(observer =>
        {
            var cancellation = new CancellationTokenSource();
            // the watches of several namespaces emit concurrently, which an observer does not support
            var synchronized = watched.Count > 1 ? System.Reactive.Observer.Synchronize(observer) : observer;
            foreach (var (watchedNamespace, resourceVersion) in watched) _ = this.PumpWatchAsync(group, version, plural, watchedNamespace, selectors, resourceVersion, synchronized, cancellation.Token);
            return Disposable.Create(() =>
            {
                cancellation.Cancel();
                cancellation.Dispose();
            });
        }).Publish().RefCount();
        return new ResourceWatch(observable, true);
    }

    /// <inheritdoc/>
    public override Task<IResource> PatchResourceAsync(Patch patch, string group, string version, string plural, string name, string? @namespace = null, string? resourceVersion = null, bool dryRun = false, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(patch);
        return this.UpdateAsync(group, version, plural, name, @namespace, null, resourceVersion, false, current => this.ApplyPatchAsync(patch, current, cancellationToken), dryRun, cancellationToken);
    }

    /// <inheritdoc/>
    public override Task<IResource> PatchSubResourceAsync(Patch patch, string group, string version, string plural, string name, string subResource, string? @namespace = null, string? resourceVersion = null, bool dryRun = false, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(patch);
        EnsureSupported(group, version, plural, name, subResource, @namespace);
        return this.UpdateAsync(group, version, plural, name, @namespace, subResource, resourceVersion, false, current => this.ApplyPatchAsync(patch, current, cancellationToken), dryRun, cancellationToken);
    }

    /// <inheritdoc/>
    public override Task<IResource> ReplaceResourceAsync(IResource resource, string group, string version, string plural, string name, string? @namespace = null, bool dryRun = false, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(resource);
        var replacement = resource.ConvertTo<Resource>()!;
        return this.UpdateAsync(group, version, plural, name, @namespace, null, replacement.Metadata.ResourceVersion, true, _ => Task.FromResult(replacement), dryRun, cancellationToken);
    }

    /// <inheritdoc/>
    public override Task<IResource> ReplaceSubResourceAsync(IResource resource, string group, string version, string plural, string name, string subResource, string? @namespace = null, bool dryRun = false, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(resource);
        EnsureSupported(group, version, plural, name, subResource, @namespace);
        var replacement = resource.ConvertTo<Resource>()!;
        return this.UpdateAsync(group, version, plural, name, @namespace, subResource, replacement.Metadata.ResourceVersion, true, _ => Task.FromResult(replacement), dryRun, cancellationToken);
    }

    /// <inheritdoc/>
    public override async Task<IResource> DeleteResourceAsync(string group, string version, string plural, string name, string? @namespace = null, bool dryRun = false, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var reference = new ResourceReference(new(group, version, plural), name, @namespace);
        if (IsDefinition(group, plural)) throw new ProblemDetailsException(DefinitionsAreReadOnly(name));
        this.EnsureServed(group, plural, name, @namespace);
        var resource = await this.GetResourceAsync(group, version, plural, name, @namespace, cancellationToken).ConfigureAwait(false) ?? throw new ProblemDetailsException(ResourceProblemDetails.ResourceNotFound(reference));
        if (IsNamespace(group, plural))
        {
            // the Kubernetes namespace is kept, with whatever else it holds: it only stops being a namespace of Cloud Streams
            if (!dryRun) await this.LabelNamespaceAsync(this.ToKubernetesNamespace(name)!, null, reference, cancellationToken).ConfigureAwait(false);
            return resource;
        }
        var kubernetesNamespace = this.ToKubernetesNamespace(@namespace);
        await this.RunAsync(() => kubernetesNamespace == null
            ? this.Kubernetes.CustomObjects.DeleteClusterCustomObjectAsync(group, version, plural, name, dryRun: dryRun ? DryRunAll : null, cancellationToken: cancellationToken)
            : this.Kubernetes.CustomObjects.DeleteNamespacedCustomObjectAsync(group, version, kubernetesNamespace, plural, name, dryRun: dryRun ? DryRunAll : null, cancellationToken: cancellationToken), reference).ConfigureAwait(false);
        return resource;
    }

    #endregion

    #region Updates

    /// <summary>
    /// Updates the specified resource, or its sub-resource, with optimistic concurrency
    /// </summary>
    /// <param name="group">The API group of the resource to update</param>
    /// <param name="version">The version of the resource to update</param>
    /// <param name="plural">The plural name of the type of the resource to update</param>
    /// <param name="name">The name of the resource to update</param>
    /// <param name="namespace">The namespace of the resource to update, if any</param>
    /// <param name="subResource">The sub-resource to update, if any. Only <c>status</c> is supported</param>
    /// <param name="expectedVersion">The version the resource is expected to be at, if any</param>
    /// <param name="versionRequired">A boolean indicating whether the resource must be at the expected version even if none has been specified, which is the case of replacements</param>
    /// <param name="update">A function used to compute the updated resource from its current state</param>
    /// <param name="dryRun">A boolean indicating whether to only validate the update, without persisting it</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The updated resource</returns>
    /// <remarks>
    /// The update is written with the version it was computed from, which the API server checks: an update that expects a version fails if the resource changed in the
    /// meantime, and any other one is computed again from the new state
    /// </remarks>
    protected virtual async Task<IResource> UpdateAsync(string group, string version, string plural, string name, string? @namespace, string? subResource, string? expectedVersion, bool versionRequired, Func<Resource, Task<Resource>> update, bool dryRun, CancellationToken cancellationToken)
    {
        var reference = new ResourceReference(new(group, version, plural), name, @namespace);
        if (IsDefinition(group, plural)) throw new ProblemDetailsException(DefinitionsAreReadOnly(name));
        this.EnsureServed(group, plural, name, @namespace);
        for (var attempt = 1; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = (Resource?)(await this.GetResourceAsync(group, version, plural, name, @namespace, cancellationToken).ConfigureAwait(false))?.ConvertTo<Resource>()
                ?? throw new ProblemDetailsException(ResourceProblemDetails.ResourceNotFound(reference));
            if ((versionRequired || !string.IsNullOrWhiteSpace(expectedVersion)) && !string.Equals(current.Metadata.ResourceVersion, expectedVersion, StringComparison.Ordinal))
                throw new ProblemDetailsException(ResourceProblemDetails.ResourceOptimisticConcurrencyCheckFailed(reference, expectedVersion!, current.Metadata.ResourceVersion!));
            var updated = await update(current).ConfigureAwait(false);
            // the API server checks the version the update was computed from
            updated.Metadata.ResourceVersion = current.Metadata.ResourceVersion;
            try
            {
                if (IsNamespace(group, plural)) return await this.WriteNamespaceAsync(updated, reference, dryRun, cancellationToken).ConfigureAwait(false);
                var body = this.ToKubernetes(updated, group, version);
                var kubernetesNamespace = this.ToKubernetesNamespace(@namespace);
                var isStatus = subResource == "status";
                var result = await this.RunAsync(() => (kubernetesNamespace, isStatus) switch
                {
                    (null, false) => this.Kubernetes.CustomObjects.ReplaceClusterCustomObjectAsync(body, group, version, plural, name, dryRun ? DryRunAll : null, cancellationToken: cancellationToken),
                    (null, true) => this.Kubernetes.CustomObjects.ReplaceClusterCustomObjectStatusAsync(body, group, version, plural, name, dryRun ? DryRunAll : null, cancellationToken: cancellationToken),
                    (_, false) => this.Kubernetes.CustomObjects.ReplaceNamespacedCustomObjectAsync(body, group, version, kubernetesNamespace, plural, name, dryRun ? DryRunAll : null, cancellationToken: cancellationToken),
                    (_, true) => this.Kubernetes.CustomObjects.ReplaceNamespacedCustomObjectStatusAsync(body, group, version, kubernetesNamespace, plural, name, dryRun ? DryRunAll : null, cancellationToken: cancellationToken)
                }, reference).ConfigureAwait(false);
                return this.ToResource(result);
            }
            catch (ProblemDetailsException ex) when (ex.Problem.Status == (int)HttpStatusCode.Conflict && ex.Problem.Type != AtomicRedisDatabase.PatchConflictProblemType && !versionRequired && string.IsNullOrWhiteSpace(expectedVersion) && attempt < MaxUpdateAttempts)
            {
                // the resource has changed since it was read: the update is computed again from its new state
                await Task.Delay(Random.Shared.Next(1, Math.Min(200, 2 << Math.Min(attempt, 16))), cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Applies the specified patch to the specified resource
    /// </summary>
    /// <param name="patch">The patch to apply</param>
    /// <param name="resource">The resource to patch</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The patched resource</returns>
    /// <remarks>Changes to the status of a resource replaced as a whole, or to anything but the status of its status sub-resource, are ignored by the API server</remarks>
    protected virtual async Task<Resource> ApplyPatchAsync(Patch patch, Resource resource, CancellationToken cancellationToken)
    {
        if (patch.Type == PatchType.JsonPatch)
        {
            // the generic handler returns the resource unchanged when an operation fails, which would make a failed 'test' operation, used to test and set offsets,
            // indistinguishable from a no-op: the patch applies to the JSON of the resource itself, as it does in the Redis database (see #161)
            var jsonPatch = patch.Document as JsonPatch ?? this.JsonSerializer.Deserialize<JsonPatch>(this.JsonSerializer.SerializeToText(patch.Document))!;
            var result = jsonPatch.Apply(JsonNode.Parse(this.JsonSerializer.SerializeToText(resource)));
            if (!result.IsSuccess) throw new ProblemDetailsException(new(AtomicRedisDatabase.PatchConflictProblemType, "Conflict", (int)HttpStatusCode.Conflict, $"The patch cannot be applied to the current state of the resource '{resource}': {result.Error}"));
            return this.JsonSerializer.Deserialize<Resource>(result.Result!.ToJsonString())!;
        }
        var handler = this.PatchHandlers.FirstOrDefault(h => h.Supports(patch.Type)) ?? throw new NullReferenceException($"Failed to find a registered service used to handle patches of type '{patch.Type}'");
        return (await handler.ApplyPatchAsync(patch.Document, resource, cancellationToken).ConfigureAwait(false))!;
    }

    static void EnsureSupported(string group, string version, string plural, string name, string subResource, string? @namespace)
    {
        if (subResource != "status") throw new ProblemDetailsException(ResourceProblemDetails.UnsupportedSubResource(new SubResourceReference(new(group, version, plural), name, subResource, @namespace)));
    }

    #endregion

    #region Custom objects

    /// <summary>
    /// Describes a page of resources
    /// </summary>
    /// <param name="Items">The resources of the page</param>
    /// <param name="Continue">The token of the next page, if any</param>
    /// <param name="ResourceVersion">The version of the list, which watches start from</param>
    protected record ResourcePage(IReadOnlyList<Resource> Items, string? Continue, string? ResourceVersion);

    /// <summary>
    /// Lists a page of the specified resources
    /// </summary>
    protected virtual async Task<ResourcePage> ListPageAsync(string group, string version, string plural, string? @namespace, IEnumerable<LabelSelector>? labelSelectors, int? limit, string? continuationToken, CancellationToken cancellationToken)
    {
        var labelSelector = labelSelectors == null ? null : string.Join(',', labelSelectors.Select(s => s.ToString()));
        if (string.IsNullOrWhiteSpace(labelSelector)) labelSelector = null;
        if (IsDefinition(group, plural)) return await this.ListDefinitionsAsync(cancellationToken).ConfigureAwait(false);
        if (IsNamespace(group, plural))
        {
            if (this.ServedNamespaces != null) return await this.ListServedNamespacesAsync(labelSelectors, cancellationToken).ConfigureAwait(false);
            return await this.ListNamespacesAsync(labelSelector, limit, continuationToken, cancellationToken).ConfigureAwait(false);
        }
        this.EnsureServed(group, plural, null, @namespace);
        if (this.SpansServedNamespaces(group, plural, @namespace)) return await this.ListServedNamespacesPageAsync(group, version, plural, labelSelectors, limit, continuationToken, cancellationToken).ConfigureAwait(false);
        var reference = new ResourceReference(new(group, version, plural), "*", @namespace);
        var kubernetesNamespace = this.ToKubernetesNamespace(@namespace);
        var result = await this.RunAsync(() => kubernetesNamespace == null
            ? this.Kubernetes.CustomObjects.ListClusterCustomObjectAsync(group, version, plural, continueParameter: continuationToken, labelSelector: labelSelector, limit: limit, cancellationToken: cancellationToken)
            : this.Kubernetes.CustomObjects.ListNamespacedCustomObjectAsync(group, version, kubernetesNamespace, plural, continueParameter: continuationToken, labelSelector: labelSelector, limit: limit, cancellationToken: cancellationToken), reference).ConfigureAwait(false);
        var list = ToNode(result);
        var items = list["items"]?.AsArray().OfType<JsonObject>().Select(this.ToResource).ToList() ?? [];
        var next = list["metadata"]?["continue"]?.GetValue<string>();
        return new(items, string.IsNullOrWhiteSpace(next) ? null : next, list["metadata"]?["resourceVersion"]?.GetValue<string>());
    }

    /// <summary>
    /// Lists a page of the specified resources across the served namespaces, one namespace at a time, since the process may not list them cluster-wide
    /// </summary>
    /// <remarks>The continuation token is the index of the namespace the next page starts in, followed by a colon and the continuation token of that namespace, if any</remarks>
    async Task<ResourcePage> ListServedNamespacesPageAsync(string group, string version, string plural, IEnumerable<LabelSelector>? labelSelectors, int? limit, string? continuationToken, CancellationToken cancellationToken)
    {
        var served = this.ServedNamespaces!;
        var index = 0;
        string? token = null;
        if (!string.IsNullOrWhiteSpace(continuationToken))
        {
            var separator = continuationToken.IndexOf(':');
            if (separator < 1 || !int.TryParse(continuationToken[..separator], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out index) || index >= served.Count)
                throw new ProblemDetailsException(new(new Uri("https://cloud-streams.io/problems/invalid-continuation-token"), "Bad Request", (int)HttpStatusCode.BadRequest, $"The continuation token '{continuationToken}' is invalid"));
            token = separator == continuationToken.Length - 1 ? null : continuationToken[(separator + 1)..];
        }
        var items = new List<Resource>();
        string? resourceVersion = null;
        while (index < served.Count && (limit == null || items.Count < limit))
        {
            var page = await this.ListPageAsync(group, version, plural, served[index], labelSelectors, limit == null ? null : limit - items.Count, token, cancellationToken).ConfigureAwait(false);
            items.AddRange(page.Items);
            resourceVersion = page.ResourceVersion ?? resourceVersion;
            if (page.Continue != null) token = page.Continue;
            else
            {
                index++;
                token = null;
            }
        }
        return new(items, index < served.Count ? $"{index}:{token}" : null, resourceVersion);
    }

    /// <summary>
    /// Gets the current version of the specified resources, which a watch starts from so that it only emits the changes made afterwards
    /// </summary>
    async Task<string?> GetCurrentVersionAsync(string group, string version, string plural, string? @namespace, IEnumerable<LabelSelector>? labelSelectors, CancellationToken cancellationToken)
    {
        // the served namespaces are read one by one, which gives no version: that of the list of Kubernetes namespaces is used
        if (IsNamespace(group, plural) && this.ServedNamespaces != null)
            return (await this.RunAsync(() => this.Kubernetes.CoreV1.ListNamespaceAsync(labelSelector: $"{this.Options.NamespaceLabel}=true", limit: 1, cancellationToken: cancellationToken), null).ConfigureAwait(false)).Metadata?.ResourceVersion;
        return (await this.ListPageAsync(group, version, plural, @namespace, labelSelectors, 1, null, cancellationToken).ConfigureAwait(false)).ResourceVersion;
    }

    async Task<JsonObject?> GetCustomObjectAsync(string group, string version, string plural, string name, string? @namespace, CancellationToken cancellationToken)
    {
        var reference = new ResourceReference(new(group, version, plural), name, @namespace);
        var kubernetesNamespace = this.ToKubernetesNamespace(@namespace);
        try
        {
            var result = await this.RunAsync(() => kubernetesNamespace == null
                ? this.Kubernetes.CustomObjects.GetClusterCustomObjectAsync(group, version, plural, name, cancellationToken)
                : this.Kubernetes.CustomObjects.GetNamespacedCustomObjectAsync(group, version, kubernetesNamespace, plural, name, cancellationToken), reference).ConfigureAwait(false);
            return ToNode(result);
        }
        catch (ProblemDetailsException ex) when (ex.Problem.Status == (int)HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    async Task PumpWatchAsync(string group, string version, string plural, string? @namespace, IReadOnlyList<LabelSelector>? labelSelectors, string? resourceVersion, IObserver<IResourceWatchEvent> observer, CancellationToken cancellationToken)
    {
        var labelSelector = labelSelectors == null || labelSelectors.Count == 0 ? null : string.Join(',', labelSelectors.Select(s => s.ToString()));
        var kubernetesNamespace = this.ToKubernetesNamespace(@namespace);
        var isNamespace = IsNamespace(group, plural);
        if (isNamespace) labelSelector = string.IsNullOrWhiteSpace(labelSelector) ? $"{this.Options.NamespaceLabel}=true" : $"{this.Options.NamespaceLabel}=true,{labelSelector}";
        var failures = 0;
        var watchTimeout = this.Options.WatchTimeout.HasValue ? (int?)Math.Max(1, (int)this.Options.WatchTimeout.Value.TotalSeconds) : null;
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (isNamespace)
                {
                    var namespaces = this.Kubernetes.CoreV1.ListNamespaceWithHttpMessagesAsync(allowWatchBookmarks: true, labelSelector: labelSelector, resourceVersion: resourceVersion, timeoutSeconds: watchTimeout, watch: true, cancellationToken: cancellationToken);
                    await foreach (var (type, value) in namespaces.WatchAsync<V1Namespace, V1NamespaceList>(cancellationToken: cancellationToken).ConfigureAwait(false))
                    {
                        resourceVersion = value.Metadata?.ResourceVersion ?? resourceVersion;
                        if (type is WatchEventType.Bookmark or WatchEventType.Error) continue;
                        var changed = this.ToNamespace(value);
                        // the namespaces the process does not serve do not exist for it
                        if (!this.IsServed(changed.GetName())) continue;
                        observer.OnNext(new ResourceWatchEvent(ToWatchEventType(type), changed));
                        failures = 0;
                    }
                }
                else
                {
                    var objects = kubernetesNamespace == null
                        ? this.Kubernetes.CustomObjects.ListClusterCustomObjectWithHttpMessagesAsync(group, version, plural, allowWatchBookmarks: true, labelSelector: labelSelector, resourceVersion: resourceVersion, timeoutSeconds: watchTimeout, watch: true, cancellationToken: cancellationToken)
                        : this.Kubernetes.CustomObjects.ListNamespacedCustomObjectWithHttpMessagesAsync(group, version, kubernetesNamespace, plural, allowWatchBookmarks: true, labelSelector: labelSelector, resourceVersion: resourceVersion, timeoutSeconds: watchTimeout, watch: true, cancellationToken: cancellationToken);
                    await foreach (var (type, value) in objects.WatchAsync<JsonElement, object>(cancellationToken: cancellationToken).ConfigureAwait(false))
                    {
                        var node = JsonObject.Create(value)!;
                        if (type == WatchEventType.Error)
                        {
                            // '410 Gone': the version has expired, and the watch starts over from the current one
                            if (node["code"]?.GetValue<int>() == (int)HttpStatusCode.Gone) resourceVersion = null;
                            break;
                        }
                        resourceVersion = node["metadata"]?["resourceVersion"]?.GetValue<string>() ?? resourceVersion;
                        if (type == WatchEventType.Bookmark) continue;
                        observer.OnNext(new ResourceWatchEvent(ToWatchEventType(type), this.ToResource(node)));
                        failures = 0;
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (HttpOperationException ex) when (ex.Response.StatusCode == HttpStatusCode.Gone)
            {
                resourceVersion = null;
            }
            catch (Exception ex)
            {
                failures++;
                this.Logger.LogWarning(ex, "The watch of the resources '{plural}.{group}' failed: it will be resumed (attempt {attempt})", plural, group, failures);
                try { await Task.Delay(TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, Math.Min(failures, 5)))), cancellationToken).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
            }
            // a watch that starts over from no version would emit every existing resource as added: it starts from the current version instead
            if (resourceVersion == null && !cancellationToken.IsCancellationRequested)
            {
                try { resourceVersion = await this.GetCurrentVersionAsync(group, version, plural, @namespace, labelSelectors, cancellationToken).ConfigureAwait(false); }
                catch (Exception ex) when (ex is not OperationCanceledException) { this.Logger.LogWarning(ex, "Failed to read the current version of the resources '{plural}.{group}'", plural, group); }
            }
        }
    }

    static ResourceWatchEventType ToWatchEventType(WatchEventType type) => type switch
    {
        WatchEventType.Added => ResourceWatchEventType.Created,
        WatchEventType.Modified => ResourceWatchEventType.Updated,
        WatchEventType.Deleted => ResourceWatchEventType.Deleted,
        _ => throw new NotSupportedException($"The watch event type '{type}' is not supported")
    };

    #endregion

    #region Definitions

    /// <summary>
    /// Gets the specified resource definition, if its CustomResourceDefinition is installed
    /// </summary>
    /// <remarks>The definitions of Cloud Streams are returned, rather than the CRDs they were generated from, since their schemas are richer, e.g. for the dashboard's editor</remarks>
    protected virtual async Task<IResource?> GetDefinitionAsync(string name, CancellationToken cancellationToken)
    {
        var namespaceDefinition = new NamespaceDefinition();
        if (name == namespaceDefinition.GetName()) return namespaceDefinition;
        try
        {
            var crd = await this.RunAsync(() => this.Kubernetes.ApiextensionsV1.ReadCustomResourceDefinitionAsync(name, cancellationToken: cancellationToken), new ResourceReference(new(ResourceDefinition.ResourceGroup, ResourceDefinition.ResourceVersion, ResourceDefinition.ResourcePlural), name)).ConfigureAwait(false);
            return this.ToDefinition(crd);
        }
        catch (ProblemDetailsException ex) when (ex.Problem.Status == (int)HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    async Task<ResourcePage> ListDefinitionsAsync(CancellationToken cancellationToken)
    {
        var crds = await this.RunAsync(() => this.Kubernetes.ApiextensionsV1.ListCustomResourceDefinitionAsync(cancellationToken: cancellationToken), null).ConfigureAwait(false);
        var definitions = crds.Items.Where(c => c.Spec.Group == CloudStreamsDefaults.ResourceGroup).Select(this.ToDefinition).Append(new NamespaceDefinition()).ToList();
        return new(definitions, null, crds.Metadata?.ResourceVersion);
    }

    Resource ToDefinition(V1CustomResourceDefinition crd)
    {
        var known = CloudStreamsDefaults.Resources.Definitions.AsEnumerable().FirstOrDefault(d => d.GetName() == crd.Metadata.Name);
        var definition = known != null
            ? this.JsonSerializer.Deserialize<ResourceDefinition>(this.JsonSerializer.SerializeToText((object)known))!
            : this.JsonSerializer.Deserialize<ResourceDefinition>(this.JsonSerializer.SerializeToText(new { apiVersion = Resource.BuildApiVersion(ResourceDefinition.ResourceGroup, ResourceDefinition.ResourceVersion), kind = ResourceDefinition.ResourceKind, metadata = new { name = crd.Metadata.Name }, spec = crd.Spec }))!;
        definition.Metadata.ResourceVersion = crd.Metadata.ResourceVersion;
        definition.Metadata.CreationTimestamp = crd.Metadata.CreationTimestamp;
        return definition;
    }

    async Task<string> GetKindAsync(string group, string plural, CancellationToken cancellationToken)
    {
        if (IsDefinition(group, plural)) return ResourceDefinition.ResourceKind;
        if (IsNamespace(group, plural)) return NamespaceDefinition.ResourceKind;
        var definition = (ResourceDefinition?)await this.GetDefinitionAsync($"{plural}.{group}", cancellationToken).ConfigureAwait(false);
        return definition?.Spec.Names.Kind ?? plural;
    }

    static Neuroglia.ProblemDetails DefinitionsAreReadOnly(string name) => new(new Uri("https://cloud-streams.io/problems/read-only-definition"), "Method Not Allowed", (int)HttpStatusCode.MethodNotAllowed,
        $"The definition '{name}' is a CustomResourceDefinition, which the Helm chart of Cloud Streams installs: it cannot be changed through Cloud Streams");

    #endregion

    #region Namespaces

    async Task<IResource?> GetNamespaceAsync(string name, CancellationToken cancellationToken)
    {
        var kubernetesNamespace = await this.ReadKubernetesNamespaceAsync(this.ToKubernetesNamespace(name)!, cancellationToken).ConfigureAwait(false);
        return kubernetesNamespace == null || !this.IsEnabled(kubernetesNamespace) ? null : this.ToNamespace(kubernetesNamespace);
    }

    async Task<V1Namespace?> ReadKubernetesNamespaceAsync(string name, CancellationToken cancellationToken)
    {
        try { return await this.RunAsync(() => this.Kubernetes.CoreV1.ReadNamespaceAsync(name, cancellationToken: cancellationToken), new ResourceReference(new(NamespaceDefinition.ResourceGroup, NamespaceDefinition.ResourceVersion, NamespaceDefinition.ResourcePlural), name)).ConfigureAwait(false); }
        catch (ProblemDetailsException ex) when (ex.Problem.Status == (int)HttpStatusCode.NotFound) { return null; }
    }

    async Task<ResourcePage> ListNamespacesAsync(string? labelSelector, int? limit, string? continuationToken, CancellationToken cancellationToken)
    {
        var selector = string.IsNullOrWhiteSpace(labelSelector) ? $"{this.Options.NamespaceLabel}=true" : $"{this.Options.NamespaceLabel}=true,{labelSelector}";
        var list = await this.RunAsync(() => this.Kubernetes.CoreV1.ListNamespaceAsync(continueParameter: continuationToken, labelSelector: selector, limit: limit, cancellationToken: cancellationToken), null).ConfigureAwait(false);
        return new([.. list.Items.Select(this.ToNamespace)], string.IsNullOrWhiteSpace(list.Metadata?.ContinueProperty) ? null : list.Metadata.ContinueProperty, list.Metadata?.ResourceVersion);
    }

    /// <summary>
    /// Lists the served namespaces, read one by one, since the process may not list the Kubernetes namespaces
    /// </summary>
    async Task<ResourcePage> ListServedNamespacesAsync(IEnumerable<LabelSelector>? labelSelectors, CancellationToken cancellationToken)
    {
        var selectors = labelSelectors?.ToList();
        var namespaces = new List<Resource>();
        foreach (var name in this.ServedNamespaces!)
        {
            var kubernetesNamespace = await this.ReadKubernetesNamespaceAsync(this.ToKubernetesNamespace(name)!, cancellationToken).ConfigureAwait(false);
            if (kubernetesNamespace == null || !this.IsEnabled(kubernetesNamespace)) continue;
            // selectors apply to the labels of the Kubernetes namespace, as they do when the Kubernetes namespaces are listed
            var labelled = new Namespace() { Metadata = new() { Name = name, Labels = kubernetesNamespace.Metadata.Labels == null ? null : new Dictionary<string, string>(kubernetesNamespace.Metadata.Labels) } };
            if (selectors?.All(s => s.Selects(labelled)) == false) continue;
            namespaces.Add(this.ToNamespace(kubernetesNamespace));
        }
        return new(namespaces, null, null);
    }

    /// <summary>
    /// Makes the specified namespace a namespace of Cloud Streams: labels its Kubernetes namespace, or creates it, if allowed (see #277)
    /// </summary>
    async Task<IResource> CreateNamespaceAsync(IResource resource, IResourceReference reference, bool dryRun, CancellationToken cancellationToken)
    {
        var name = this.ToKubernetesNamespace(resource.GetName())!;
        var existing = await this.ReadKubernetesNamespaceAsync(name, cancellationToken).ConfigureAwait(false);
        if (existing != null && this.IsEnabled(existing)) throw new ProblemDetailsException(ResourceProblemDetails.ResourceAlreadyExists(reference));
        if (existing != null)
        {
            if (dryRun) return this.ToNamespace(existing);
            return this.ToNamespace(await this.LabelNamespaceAsync(name, resource.Metadata.Labels ?? new Dictionary<string, string>(), reference, cancellationToken).ConfigureAwait(false));
        }
        if (!this.Options.CreateNamespaces) throw new ProblemDetailsException(new(new Uri("https://cloud-streams.io/problems/namespace-not-found"), "Not Found", (int)HttpStatusCode.NotFound,
            $"The Kubernetes namespace '{name}' does not exist, and Cloud Streams is not configured to create namespaces: create it, then the namespace '{resource.GetName()}'"));
        var labels = new Dictionary<string, string>(resource.Metadata.Labels ?? new Dictionary<string, string>()) { [this.Options.NamespaceLabel] = "true" };
        var created = await this.RunAsync(() => this.Kubernetes.CoreV1.CreateNamespaceAsync(new V1Namespace() { Metadata = new() { Name = name, Labels = labels, Annotations = resource.Metadata.Annotations } }, dryRun ? DryRunAll : null, cancellationToken: cancellationToken), reference).ConfigureAwait(false);
        return this.ToNamespace(created);
    }

    /// <summary>
    /// Labels the specified Kubernetes namespace as a namespace of Cloud Streams, with the specified labels, or removes that label if they are null
    /// </summary>
    async Task<V1Namespace> LabelNamespaceAsync(string name, IDictionary<string, string>? labels, IResourceReference reference, CancellationToken cancellationToken)
    {
        var patch = new JsonObject() { ["metadata"] = new JsonObject() { ["labels"] = new JsonObject() } };
        var patchedLabels = patch["metadata"]!["labels"]!.AsObject();
        if (labels == null) patchedLabels[this.Options.NamespaceLabel] = null;
        else
        {
            foreach (var (key, value) in labels) patchedLabels[key] = value;
            patchedLabels[this.Options.NamespaceLabel] = "true";
        }
        return await this.RunAsync(() => this.Kubernetes.CoreV1.PatchNamespaceAsync(new V1Patch(patch.ToJsonString(), V1Patch.PatchType.MergePatch), name, cancellationToken: cancellationToken), reference).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes the labels and annotations of the specified namespace of Cloud Streams to its Kubernetes namespace, which are all a namespace holds
    /// </summary>
    async Task<IResource> WriteNamespaceAsync(Resource updated, IResourceReference reference, bool dryRun, CancellationToken cancellationToken)
    {
        var name = this.ToKubernetesNamespace(updated.GetName())!;
        var current = await this.ReadKubernetesNamespaceAsync(name, cancellationToken).ConfigureAwait(false) ?? throw new ProblemDetailsException(ResourceProblemDetails.ResourceNotFound(reference));
        if (current.Metadata.ResourceVersion != updated.Metadata.ResourceVersion) throw new ProblemDetailsException(ResourceProblemDetails.ResourceOptimisticConcurrencyCheckFailed(reference, updated.Metadata.ResourceVersion!, current.Metadata.ResourceVersion));
        current.Metadata.Labels = new Dictionary<string, string>(updated.Metadata.Labels ?? new Dictionary<string, string>()) { [this.Options.NamespaceLabel] = "true" };
        current.Metadata.Annotations = updated.Metadata.Annotations == null ? null : new Dictionary<string, string>(updated.Metadata.Annotations);
        var result = await this.RunAsync(() => this.Kubernetes.CoreV1.ReplaceNamespaceAsync(current, name, dryRun ? DryRunAll : null, cancellationToken: cancellationToken), reference).ConfigureAwait(false);
        return this.ToNamespace(result);
    }

    bool IsEnabled(V1Namespace kubernetesNamespace) => kubernetesNamespace.Metadata?.Labels?.TryGetValue(this.Options.NamespaceLabel, out var value) == true && string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);

    Resource ToNamespace(V1Namespace kubernetesNamespace)
    {
        var labels = kubernetesNamespace.Metadata.Labels?.Where(l => l.Key != this.Options.NamespaceLabel && !l.Key.StartsWith("kubernetes.io/", StringComparison.Ordinal)).ToDictionary(l => l.Key, l => l.Value);
        var resource = new Namespace()
        {
            Metadata = new()
            {
                Name = this.FromKubernetesNamespace(kubernetesNamespace.Metadata.Name),
                Labels = labels?.Count > 0 ? labels : null,
                Annotations = kubernetesNamespace.Metadata.Annotations?.Count > 0 ? new Dictionary<string, string>(kubernetesNamespace.Metadata.Annotations) : null,
                ResourceVersion = kubernetesNamespace.Metadata.ResourceVersion,
                CreationTimestamp = kubernetesNamespace.Metadata.CreationTimestamp,
                Generation = (ulong)(kubernetesNamespace.Metadata.Generation ?? 0)
            }
        };
        return resource.ConvertTo<Resource>()!;
    }

    #endregion

    #region Conversions

    /// <summary>
    /// Converts the specified resource into the custom object Kubernetes stores
    /// </summary>
    protected virtual JsonObject ToKubernetes(IResource resource, string group, string version)
    {
        var node = JsonNode.Parse(this.JsonSerializer.SerializeToText((object)resource))!.AsObject();
        // serialized as its runtime type: as an IResource, only the members of the interface would be
        node["apiVersion"] = Resource.BuildApiVersion(group, version);
        if (node["metadata"] is JsonObject metadata)
        {
            var kubernetesNamespace = this.ToKubernetesNamespace(resource.GetNamespace());
            if (kubernetesNamespace == null) metadata.Remove("namespace");
            else metadata["namespace"] = kubernetesNamespace;
            // the generation is computed by the API server, which refuses to set it
            metadata.Remove("generation");
            if (metadata["resourceVersion"] == null) metadata.Remove("resourceVersion");
        }
        return node;
    }

    /// <summary>
    /// Converts the specified custom object into a resource
    /// </summary>
    protected virtual Resource ToResource(object customObject) => this.ToResource(ToNode(customObject));

    /// <summary>
    /// Converts the specified custom object into a resource
    /// </summary>
    protected virtual Resource ToResource(JsonObject node)
    {
        if (node["metadata"] is JsonObject metadata)
        {
            if (metadata["namespace"] is JsonNode kubernetesNamespace) metadata["namespace"] = this.FromKubernetesNamespace(kubernetesNamespace.GetValue<string>());
            // fields that only Kubernetes manages are not part of the resources of Cloud Streams
            metadata.Remove("managedFields");
            metadata.Remove("uid");
            metadata.Remove("selfLink");
        }
        return this.JsonSerializer.Deserialize<Resource>(node.ToJsonString())!;
    }

    static JsonObject ToNode(object value) => value switch
    {
        JsonObject node => node,
        JsonElement element => JsonObject.Create(element) ?? throw new FormatException("The API server returned an empty object"),
        _ => JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(value))!.AsObject()
    };

    #endregion

    #region Errors

    /// <summary>
    /// Runs the specified call to the API server, and reports its errors as <see cref="ProblemDetailsException"/>s, with the statuses the Redis database reports
    /// </summary>
    protected virtual async Task<T> RunAsync<T>(Func<Task<T>> call, IResourceReference? reference)
    {
        try
        {
            return await call().ConfigureAwait(false);
        }
        catch (HttpOperationException ex)
        {
            var status = (int)ex.Response.StatusCode;
            var message = TryGetMessage(ex.Response.Content) ?? ex.Message;
            throw new ProblemDetailsException((status, reference) switch
            {
                ((int)HttpStatusCode.NotFound, not null) => ResourceProblemDetails.ResourceNotFound(reference),
                ((int)HttpStatusCode.Conflict, not null) when message.Contains("already exists", StringComparison.OrdinalIgnoreCase) => ResourceProblemDetails.ResourceAlreadyExists(reference),
                // an unprocessable resource is invalid, as the Redis database reports it
                ((int)HttpStatusCode.UnprocessableEntity, _) => new(new Uri("https://cloud-streams.io/problems/validation-failed"), "Validation Failed", (int)HttpStatusCode.BadRequest, message),
                _ => new(new Uri($"https://httpstatuses.io/{status}"), ex.Response.ReasonPhrase ?? ex.Response.StatusCode.ToString(), status, message)
            });
        }
    }

    static string? TryGetMessage(string? content)
    {
        if (string.IsNullOrWhiteSpace(content)) return null;
        try { return JsonNode.Parse(content)?["message"]?.GetValue<string>(); }
        catch (JsonException) { return null; }
    }

    #endregion

}
