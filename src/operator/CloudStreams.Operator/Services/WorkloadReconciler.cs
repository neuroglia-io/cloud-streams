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

using k8s.Autorest;
using Neuroglia.Serialization;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace CloudStreams.Operator.Services;

/// <summary>
/// Deploys the workloads of gateways and brokers from their resources, and reports their conditions (see #279). Only runs on the replica that leads the operator
/// </summary>
/// <param name="logger">The service used to perform logging</param>
/// <param name="leadership">The service that elects the replica that leads the operator</param>
/// <param name="kubernetes">The service used to interact with the Kubernetes API</param>
/// <param name="serializer">The service used to serialize and deserialize JSON</param>
/// <param name="factory">The service used to build the objects of workloads</param>
/// <param name="options">The options of the operator</param>
public class WorkloadReconciler(ILogger<WorkloadReconciler> logger, OperatorLeadership leadership, IKubernetes kubernetes, IJsonSerializer serializer, WorkloadFactory factory, IOptions<OperatorOptions> options)
    : LeaderService(logger, leadership)
{

    const string Version = "v1";

    readonly ConcurrentDictionary<string, byte> _pending = new();
    Channel<string> _queue = Channel.CreateUnbounded<string>();

    /// <summary>
    /// Gets the options of the operator
    /// </summary>
    protected OperatorOptions Options => options.Value;

    /// <inheritdoc/>
    protected override async Task RunAsLeaderAsync(CancellationToken cancellationToken)
    {
        this._pending.Clear();
        this._queue = Channel.CreateUnbounded<string>();
        var tasks = new List<Task> { this.ProcessQueueAsync(cancellationToken), this.ResyncAsync(cancellationToken), this.WatchDeploymentsAsync(cancellationToken) };
        tasks.AddRange(WorkloadKind.All.Select(kind => this.WatchResourcesAsync(kind, cancellationToken)));
        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    /// <summary>
    /// Queues the reconciliation of the specified resource, unless it is already queued
    /// </summary>
    /// <param name="kind">The kind of the resource</param>
    /// <param name="namespace">The Kubernetes namespace of the resource</param>
    /// <param name="name">The name of the resource</param>
    protected virtual void Enqueue(WorkloadKind kind, string @namespace, string name)
    {
        var key = $"{kind.Component}/{@namespace}/{name}";
        if (this._pending.TryAdd(key, 0)) this._queue.Writer.TryWrite(key);
    }

    /// <summary>
    /// Reconciles the queued resources, one at a time
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    protected virtual async Task ProcessQueueAsync(CancellationToken cancellationToken)
    {
        await foreach (var key in this._queue.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            this._pending.TryRemove(key, out _);
            var parts = key.Split('/', 3);
            var kind = WorkloadKind.FromComponent(parts[0])!;
            try { await this.ReconcileAsync(kind, parts[1], parts[2], cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                this.Logger.LogError(ex, "Failed to reconcile the {kind} '{name}' of the namespace '{namespace}'. Retrying...", kind.Component, parts[2], parts[1]);
                _ = Task.Delay(TimeSpan.FromSeconds(10), cancellationToken).ContinueWith(_ => this.Enqueue(kind, parts[1], parts[2]), TaskContinuationOptions.OnlyOnRanToCompletion);
            }
        }
    }

    /// <summary>
    /// Queues every gateway and broker at the configured interval, and updates the subjects of the cluster-wide role binding of the workloads
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    protected virtual async Task ResyncAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var subjects = new List<Rbacv1Subject>();
                var sharedSecretReaders = new List<Rbacv1Subject>();
                foreach (var kind in WorkloadKind.All)
                {
                    var list = ToNode(await kubernetes.CustomObjects.ListClusterCustomObjectAsync(CloudStreamsDefaults.ResourceGroup, Version, kind.Plural, cancellationToken: cancellationToken).ConfigureAwait(false));
                    foreach (var item in list["items"]?.AsArray().OfType<JsonObject>() ?? [])
                    {
                        var resource = ManagedResource.FromCustomObject(kind, item, serializer);
                        if (resource.IsExternal) continue;
                        subjects.Add(new("ServiceAccount", WorkloadFactory.GetObjectName(kind, resource.Name), namespaceProperty: resource.Namespace));
                        if (this.Options.SharedSecrets.IsSharedWith(factory.GetCloudStreamsNamespace(resource.Namespace))) sharedSecretReaders.Add(subjects[^1]);
                        this.Enqueue(kind, resource.Namespace, resource.Name);
                    }
                }
                await this.SyncClusterRoleBindingAsync(subjects, true, cancellationToken).ConfigureAwait(false);
                await this.SyncSharedSecretsRoleBindingAsync(sharedSecretReaders, true, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return; }
            catch (Exception ex) { this.Logger.LogError(ex, "Failed to list the gateways and brokers to reconcile"); }
            try { await Task.Delay(this.Options.ResyncInterval, cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) { return; }
        }
    }

    /// <summary>
    /// Queues the resources of the specified kind when they change
    /// </summary>
    /// <param name="kind">The kind of resources to watch</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    protected virtual Task WatchResourcesAsync(WorkloadKind kind, CancellationToken cancellationToken) => this.WatchAsync<CustomObjectMetadata, object>(
        resourceVersion => kubernetes.CustomObjects.ListClusterCustomObjectWithHttpMessagesAsync(CloudStreamsDefaults.ResourceGroup, Version, kind.Plural, allowWatchBookmarks: true, resourceVersion: resourceVersion, timeoutSeconds: 300, watch: true, cancellationToken: cancellationToken),
        item => { if (item.Metadata?.Name != null && item.Metadata.NamespaceProperty != null) this.Enqueue(kind, item.Metadata.NamespaceProperty, item.Metadata.Name); },
        $"the {kind.Plural}", cancellationToken);

    /// <summary>
    /// Queues the resources whose workload's deployment changes, so that their conditions are updated
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    protected virtual Task WatchDeploymentsAsync(CancellationToken cancellationToken) => this.WatchAsync<V1Deployment, V1DeploymentList>(
        resourceVersion => kubernetes.AppsV1.ListDeploymentForAllNamespacesWithHttpMessagesAsync(allowWatchBookmarks: true, labelSelector: $"{WorkloadFactory.OperatorLabel}={this.Options.Instance}", resourceVersion: resourceVersion, timeoutSeconds: 300, watch: true, cancellationToken: cancellationToken),
        deployment =>
        {
            var labels = deployment.Metadata?.Labels;
            if (labels == null || deployment.Metadata!.NamespaceProperty == null) return;
            foreach (var kind in WorkloadKind.All)
            {
                if (labels.TryGetValue(WorkloadFactory.ResourceLabelPrefix + kind.Component, out var name)) this.Enqueue(kind, deployment.Metadata.NamespaceProperty, name);
            }
        },
        "the deployments of the workloads", cancellationToken);

    /// <summary>
    /// Watches Kubernetes objects, resuming where the watch stopped when the API server closes it
    /// </summary>
    /// <typeparam name="T">The type of the watched objects</typeparam>
    /// <typeparam name="TList">The type of the list of the watched objects</typeparam>
    /// <param name="watch">A function that starts a watch from the specified resource version, if any</param>
    /// <param name="onChange">A function that handles a change</param>
    /// <param name="description">A description of the watched objects, for logging</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    protected virtual async Task WatchAsync<T, TList>(Func<string?, Task<HttpOperationResponse<TList>>> watch, Action<T> onChange, string description, CancellationToken cancellationToken)
        where T : IKubernetesObject<V1ObjectMeta>
    {
        string? resourceVersion = null;
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await foreach (var (type, item) in watch(resourceVersion).WatchAsync<T, TList>(cancellationToken: cancellationToken).ConfigureAwait(false))
                {
                    resourceVersion = item.Metadata?.ResourceVersion ?? resourceVersion;
                    if (type is WatchEventType.Added or WatchEventType.Modified or WatchEventType.Deleted) onChange(item);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return; }
            catch (Exception ex)
            {
                // the version has expired: the periodic resync catches up with the changes the watch missed
                if (ex is HttpOperationException { Response.StatusCode: HttpStatusCode.Gone } || ex.Message.Contains("too old", StringComparison.OrdinalIgnoreCase)) resourceVersion = null;
                else this.Logger.LogWarning(ex, "The watch of {objects} failed. Retrying...", description);
                try { await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false); }
                catch (OperationCanceledException) { return; }
            }
        }
    }

    /// <summary>
    /// Reconciles the workload of the specified resource
    /// </summary>
    /// <param name="kind">The kind of the resource</param>
    /// <param name="namespace">The Kubernetes namespace of the resource</param>
    /// <param name="name">The name of the resource</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    protected virtual async Task ReconcileAsync(WorkloadKind kind, string @namespace, string name, CancellationToken cancellationToken)
    {
        JsonObject node;
        try { node = ToNode(await kubernetes.CustomObjects.GetNamespacedCustomObjectAsync(CloudStreamsDefaults.ResourceGroup, Version, @namespace, kind.Plural, name, cancellationToken).ConfigureAwait(false)); }
        catch (HttpOperationException ex) when (ex.Response.StatusCode == HttpStatusCode.NotFound) { return; /* the objects of its workload are garbage collected with it */ }
        var resource = ManagedResource.FromCustomObject(kind, node, serializer);
        if (resource.Deleting || resource.IsExternal) return;
        V1Deployment? deployment = null;
        string? failure = null;
        try { deployment = await this.ApplyAsync(resource, cancellationToken).ConfigureAwait(false); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            failure = ex is HttpOperationException http ? $"{http.Message}: {http.Response?.Content}" : ex.Message;
            this.Logger.LogError(ex, "Failed to apply the workload of the {kind} '{name}' of the namespace '{namespace}'", kind.Component, name, @namespace);
        }
        var conditions = WorkloadStatusHelper.GetConditions(resource, deployment, failure, DateTimeOffset.UtcNow);
        var available = deployment?.Status?.AvailableReplicas ?? (failure == null ? 0 : resource.AvailableReplicas);
        if (WorkloadStatusHelper.HasChanged(resource, conditions, available))
        {
            var status = new JsonObject()
            {
                ["status"] = new JsonObject()
                {
                    ["conditions"] = JsonNode.Parse(serializer.SerializeToText(conditions)),
                    ["availableReplicas"] = available
                }
            };
            await kubernetes.CustomObjects.PatchNamespacedCustomObjectStatusAsync(new V1Patch(status, V1Patch.PatchType.MergePatch), CloudStreamsDefaults.ResourceGroup, Version, @namespace, kind.Plural, name, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        if (failure != null) throw new InvalidOperationException(failure);
    }

    /// <summary>
    /// Applies the objects of the workload of the specified resource
    /// </summary>
    /// <param name="resource">The resource to apply the workload of</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The applied deployment</returns>
    protected virtual async Task<V1Deployment> ApplyAsync(ManagedResource resource, CancellationToken cancellationToken)
    {
        var connections = new Dictionary<string, byte[]>()
        {
            ["eventstore"] = await this.ReadSecretKeyAsync(this.Options.EventStore, cancellationToken).ConfigureAwait(false),
            ["redis"] = await this.ReadSecretKeyAsync(this.Options.Redis, cancellationToken).ConfigureAwait(false)
        };
        var workload = factory.Create(resource, connections);
        var ns = resource.Namespace;
        V1Patch Apply(object body) => new(body, V1Patch.PatchType.ApplyPatch);
        await kubernetes.CoreV1.PatchNamespacedServiceAccountAsync(Apply(workload.ServiceAccount), workload.Name, ns, fieldManager: OperatorOptions.FieldManager, force: true, cancellationToken: cancellationToken).ConfigureAwait(false);
        await kubernetes.RbacAuthorizationV1.PatchNamespacedRoleBindingAsync(Apply(workload.RoleBinding), workload.Name, ns, fieldManager: OperatorOptions.FieldManager, force: true, cancellationToken: cancellationToken).ConfigureAwait(false);
        await this.SyncClusterRoleBindingAsync([new("ServiceAccount", workload.Name, namespaceProperty: ns)], false, cancellationToken).ConfigureAwait(false);
        if (this.Options.SharedSecrets.IsSharedWith(factory.GetCloudStreamsNamespace(ns))) await this.SyncSharedSecretsRoleBindingAsync([new("ServiceAccount", workload.Name, namespaceProperty: ns)], false, cancellationToken).ConfigureAwait(false);
        await kubernetes.CoreV1.PatchNamespacedSecretAsync(Apply(workload.Connections), workload.Connections.Metadata.Name, ns, fieldManager: OperatorOptions.FieldManager, force: true, cancellationToken: cancellationToken).ConfigureAwait(false);
        var deployment = await kubernetes.AppsV1.PatchNamespacedDeploymentAsync(Apply(workload.Deployment), workload.Name, ns, fieldManager: OperatorOptions.FieldManager, force: true, cancellationToken: cancellationToken).ConfigureAwait(false);
        await kubernetes.CoreV1.PatchNamespacedServiceAsync(Apply(workload.Service), workload.Name, ns, fieldManager: OperatorOptions.FieldManager, force: true, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (workload.PodDisruptionBudget != null) await kubernetes.PolicyV1.PatchNamespacedPodDisruptionBudgetAsync(Apply(workload.PodDisruptionBudget), workload.Name, ns, fieldManager: OperatorOptions.FieldManager, force: true, cancellationToken: cancellationToken).ConfigureAwait(false);
        else await IgnoreNotFoundAsync(() => kubernetes.PolicyV1.DeleteNamespacedPodDisruptionBudgetAsync(workload.Name, ns, cancellationToken: cancellationToken)).ConfigureAwait(false);
        if (workload.Ingress != null) await kubernetes.NetworkingV1.PatchNamespacedIngressAsync(Apply(workload.Ingress), workload.Name, ns, fieldManager: OperatorOptions.FieldManager, force: true, cancellationToken: cancellationToken).ConfigureAwait(false);
        else if (resource.Kind == WorkloadKind.Gateway) await IgnoreNotFoundAsync(() => kubernetes.NetworkingV1.DeleteNamespacedIngressAsync(workload.Name, ns, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return deployment;
    }

    /// <summary>
    /// Reads the specified key of a secret of the operator's namespace
    /// </summary>
    /// <param name="reference">The secret and the key to read</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The value of the key</returns>
    protected virtual async Task<byte[]> ReadSecretKeyAsync(SecretKeyReference reference, CancellationToken cancellationToken)
    {
        V1Secret secret;
        try { secret = await kubernetes.CoreV1.ReadNamespacedSecretAsync(reference.Name, this.Options.Namespace, cancellationToken: cancellationToken).ConfigureAwait(false); }
        catch (HttpOperationException ex) when (ex.Response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException($"The secret '{reference.Name}' of the namespace '{this.Options.Namespace}', which holds a connection string of the workloads, does not exist");
        }
        if (secret.Data?.TryGetValue(reference.Key, out var value) != true || value == null) throw new InvalidOperationException($"The secret '{reference.Name}' of the namespace '{this.Options.Namespace}' has no '{reference.Key}' key");
        return value;
    }

    /// <summary>
    /// Makes the cluster-wide role binding of the workloads bind the specified service accounts, which grants them the cluster-wide reads they need
    /// </summary>
    /// <param name="subjects">The service accounts to bind</param>
    /// <param name="exclusive">A boolean indicating whether or not the binding must bind the specified service accounts only, removing the others</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    /// <remarks>The binding is created by the chart: the operator may only update it, since Kubernetes can't restrict the creation of objects by name</remarks>
    protected virtual async Task SyncClusterRoleBindingAsync(IReadOnlyCollection<Rbacv1Subject> subjects, bool exclusive, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            V1ClusterRoleBinding binding;
            try { binding = await kubernetes.RbacAuthorizationV1.ReadClusterRoleBindingAsync(this.Options.WorkloadClusterRoleBinding, cancellationToken: cancellationToken).ConfigureAwait(false); }
            catch (HttpOperationException ex) when (ex.Response.StatusCode == HttpStatusCode.NotFound)
            {
                throw new InvalidOperationException($"The ClusterRoleBinding '{this.Options.WorkloadClusterRoleBinding}', which grants the workloads the cluster-wide reads they need, does not exist: it is installed by the chart");
            }
            if (MergeSubjects(binding.Subjects, subjects, exclusive) is not List<Rbacv1Subject> merged) return;
            binding.Subjects = merged;
            try
            {
                await kubernetes.RbacAuthorizationV1.ReplaceClusterRoleBindingAsync(binding, binding.Metadata.Name, fieldManager: OperatorOptions.FieldManager, cancellationToken: cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (HttpOperationException ex) when (ex.Response.StatusCode == HttpStatusCode.Conflict && attempt < 5) { }
        }
    }

    /// <summary>
    /// Makes the role binding of the shared secrets bind the specified service accounts, which lets them read the secrets of the operator's namespace (see #281)
    /// </summary>
    /// <param name="subjects">The service accounts to bind</param>
    /// <param name="exclusive">A boolean indicating whether or not the binding must bind the specified service accounts only, removing the others</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    /// <remarks>The binding is created by the chart: the operator only updates it</remarks>
    protected virtual async Task SyncSharedSecretsRoleBindingAsync(IReadOnlyCollection<Rbacv1Subject> subjects, bool exclusive, CancellationToken cancellationToken)
    {
        if (!this.Options.SharedSecrets.Enabled) return;
        for (var attempt = 1; ; attempt++)
        {
            V1RoleBinding binding;
            try { binding = await kubernetes.RbacAuthorizationV1.ReadNamespacedRoleBindingAsync(this.Options.SharedSecrets.RoleBinding, this.Options.Namespace, cancellationToken: cancellationToken).ConfigureAwait(false); }
            catch (HttpOperationException ex) when (ex.Response.StatusCode == HttpStatusCode.NotFound)
            {
                throw new InvalidOperationException($"The RoleBinding '{this.Options.SharedSecrets.RoleBinding}' of the namespace '{this.Options.Namespace}', which lets workloads read the shared secrets, does not exist: it is installed by the chart");
            }
            if (MergeSubjects(binding.Subjects, subjects, exclusive) is not List<Rbacv1Subject> merged) return;
            binding.Subjects = merged;
            try
            {
                await kubernetes.RbacAuthorizationV1.ReplaceNamespacedRoleBindingAsync(binding, binding.Metadata.Name, binding.Metadata.NamespaceProperty, fieldManager: OperatorOptions.FieldManager, cancellationToken: cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (HttpOperationException ex) when (ex.Response.StatusCode == HttpStatusCode.Conflict && attempt < 5) { }
        }
    }

    /// <summary>
    /// Merges the specified subjects into those of a binding
    /// </summary>
    /// <param name="current">The current subjects of the binding, if any</param>
    /// <param name="subjects">The subjects to bind</param>
    /// <param name="exclusive">A boolean indicating whether or not the binding must bind the specified subjects only</param>
    /// <returns>The subjects the binding must have, or null if they're unchanged</returns>
    public static List<Rbacv1Subject>? MergeSubjects(IList<Rbacv1Subject>? current, IReadOnlyCollection<Rbacv1Subject> subjects, bool exclusive)
    {
        static string Key(Rbacv1Subject subject) => $"{subject.NamespaceProperty}/{subject.Name}";
        current ??= [];
        var wanted = exclusive ? subjects.DistinctBy(Key).ToList() : current.Concat(subjects).DistinctBy(Key).ToList();
        if (current.Count == wanted.Count && current.Select(Key).ToHashSet().SetEquals(wanted.Select(Key))) return null;
        return [.. wanted.OrderBy(Key)];
    }

    /// <summary>
    /// Runs the specified call, ignoring the 404 it may fail with
    /// </summary>
    /// <param name="call">The call to run</param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    protected static async Task IgnoreNotFoundAsync(Func<Task> call)
    {
        try { await call().ConfigureAwait(false); }
        catch (HttpOperationException ex) when (ex.Response.StatusCode == HttpStatusCode.NotFound) { }
    }

    /// <summary>
    /// Converts the specified custom object, as the Kubernetes client returns it, into a <see cref="JsonObject"/>
    /// </summary>
    /// <param name="customObject">The custom object to convert</param>
    /// <returns>The converted custom object</returns>
    protected static JsonObject ToNode(object customObject) => customObject switch
    {
        JsonObject node => node,
        JsonElement element => JsonNode.Parse(element.GetRawText())!.AsObject(),
        _ => JsonNode.Parse(KubernetesJson.Serialize(customObject))!.AsObject()
    };

}
