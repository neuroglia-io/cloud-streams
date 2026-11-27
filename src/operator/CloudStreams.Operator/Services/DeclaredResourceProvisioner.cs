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
using CloudStreams.Operator.Admission;
using k8s.Autorest;

namespace CloudStreams.Operator.Services;

/// <summary>
/// Creates the gateways and brokers declared in the operator's configuration, e.g. by the 'gateways' and 'brokers' lists of the Helm chart, when they're missing.
/// It never updates nor deletes them, so that they may be changed with kubectl or the dashboard (see #283). Only runs on the replica that leads the operator
/// </summary>
/// <remarks>
/// <para>The operator creates them, rather than the chart, because a resource the chart created would be refused by the admission webhook until the operator serves it.</para>
/// <para>Each declaration is only provisioned once: the operator records it in a ConfigMap, so that a declared resource deleted with kubectl isn't created again,
/// until its declaration changes</para>
/// </remarks>
/// <param name="logger">The service used to perform logging</param>
/// <param name="leadership">The service that elects the replica that leads the operator</param>
/// <param name="kubernetes">The service used to interact with the Kubernetes API</param>
/// <param name="webhook">The admission webhook, which validates the declared resources before they're created</param>
/// <param name="store">The options of the Kubernetes store of resources</param>
/// <param name="options">The options of the operator</param>
public class DeclaredResourceProvisioner(ILogger<DeclaredResourceProvisioner> logger, OperatorLeadership leadership, IKubernetes kubernetes, AdmissionWebhook webhook, KubernetesResourceStoreOptions store, IOptions<OperatorOptions> options)
    : LeaderService(logger, leadership)
{

    /// <inheritdoc/>
    protected override async Task RunAsLeaderAsync(CancellationToken cancellationToken)
    {
        var path = options.Value.DeclaredResourcesPath;
        if (string.IsNullOrWhiteSpace(path)) return;
        if (!File.Exists(path))
        {
            this.Logger.LogWarning("The file of declared gateways and brokers '{path}' does not exist", path);
            return;
        }
        var resources = JsonNode.Parse(await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false)) as JsonArray ?? [];
        foreach (var resource in resources.OfType<JsonObject>())
        {
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    var key = GetRecordKey(resource);
                    var checksum = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(resource.ToJsonString()))).ToLowerInvariant();
                    var record = await this.ReadRecordAsync(cancellationToken).ConfigureAwait(false);
                    if (record.Data?.TryGetValue(key, out var provisioned) == true && provisioned == checksum) break;
                    await this.ProvisionAsync(resource, cancellationToken).ConfigureAwait(false);
                    record.Data ??= new Dictionary<string, string>();
                    record.Data[key] = checksum;
                    await kubernetes.CoreV1.ReplaceNamespacedConfigMapAsync(record, record.Metadata.Name, record.Metadata.NamespaceProperty, cancellationToken: cancellationToken).ConfigureAwait(false);
                    break;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return; }
                catch (Exception ex)
                {
                    var delay = TimeSpan.FromSeconds(Math.Min(Math.Pow(2, attempt), 60));
                    this.Logger.LogError(ex, "Failed to create the declared {kind} '{name}' (attempt {attempt}). Retrying in {delay}...", resource["kind"], resource["metadata"]?["name"], attempt, delay);
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }

    /// <summary>
    /// Gets the name of the ConfigMap, in the operator's namespace, that records the declarations the operator provisioned
    /// </summary>
    protected virtual string RecordName => $"{options.Value.Instance}-operator-declarations";

    /// <summary>
    /// Gets the key of the specified declaration in the record of provisioned declarations
    /// </summary>
    /// <param name="resource">The declared resource</param>
    /// <returns>The key of the declaration, e.g. 'Gateway.tenant-a.orders'</returns>
    public static string GetRecordKey(JsonObject resource) => $"{resource["kind"]}.{resource["metadata"]?["namespace"]?.GetValue<string>() ?? "_"}.{resource["metadata"]?["name"]}";

    /// <summary>
    /// Reads the record of the declarations the operator provisioned, creating it if it doesn't exist
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The record of provisioned declarations</returns>
    protected virtual async Task<V1ConfigMap> ReadRecordAsync(CancellationToken cancellationToken)
    {
        try { return await kubernetes.CoreV1.ReadNamespacedConfigMapAsync(this.RecordName, options.Value.Namespace, cancellationToken: cancellationToken).ConfigureAwait(false); }
        catch (HttpOperationException ex) when (ex.Response.StatusCode == HttpStatusCode.NotFound) { }
        var record = new V1ConfigMap()
        {
            Metadata = new()
            {
                Name = this.RecordName,
                NamespaceProperty = options.Value.Namespace,
                Labels = new Dictionary<string, string>() { { "app.kubernetes.io/managed-by", WorkloadFactory.ManagedBy } }
            },
            Data = new Dictionary<string, string>()
        };
        try { return await kubernetes.CoreV1.CreateNamespacedConfigMapAsync(record, options.Value.Namespace, cancellationToken: cancellationToken).ConfigureAwait(false); }
        catch (HttpOperationException ex) when (ex.Response.StatusCode == HttpStatusCode.Conflict)
        {
            return await kubernetes.CoreV1.ReadNamespacedConfigMapAsync(this.RecordName, options.Value.Namespace, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Creates the specified resource, and its namespace, if they're missing
    /// </summary>
    /// <param name="resource">The resource to create</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    protected virtual async Task ProvisionAsync(JsonObject resource, CancellationToken cancellationToken)
    {
        var kind = WorkloadKind.All.FirstOrDefault(k => k.Kind == resource["kind"]?.GetValue<string>()) ?? throw new InvalidOperationException($"Only gateways and brokers may be declared, not '{resource["kind"]}'");
        var name = resource["metadata"]?["name"]?.GetValue<string>() ?? throw new InvalidOperationException("A declared resource has no name");
        var @namespace = resource["metadata"]?["namespace"]?.GetValue<string>() ?? store.DefaultNamespace;
        resource["metadata"]!["namespace"] = @namespace;
        try
        {
            await kubernetes.CustomObjects.GetNamespacedCustomObjectAsync(CloudStreamsDefaults.ResourceGroup, "v1", @namespace, kind.Plural, name, cancellationToken).ConfigureAwait(false);
            return;
        }
        catch (HttpOperationException ex) when (ex.Response.StatusCode == HttpStatusCode.NotFound) { }
        await this.EnsureNamespaceAsync(@namespace, cancellationToken).ConfigureAwait(false);
        var review = await webhook.ReviewAsync(new AdmissionReview()
        {
            Request = new()
            {
                Uid = Guid.NewGuid().ToString("N"),
                Operation = "CREATE",
                Resource = new() { Group = CloudStreamsDefaults.ResourceGroup, Version = "v1", Resource = kind.Plural },
                Name = name,
                Namespace = @namespace,
                Object = resource
            }
        }, cancellationToken).ConfigureAwait(false);
        if (review.Response?.Allowed != true) throw new InvalidOperationException(review.Response?.Status?.Message ?? $"The declared {kind.Component} '{name}' is invalid");
        try
        {
            await kubernetes.CustomObjects.CreateNamespacedCustomObjectAsync(resource, CloudStreamsDefaults.ResourceGroup, "v1", @namespace, kind.Plural, fieldManager: OperatorOptions.FieldManager, cancellationToken: cancellationToken).ConfigureAwait(false);
            this.Logger.LogInformation("Created the declared {kind} '{name}' of the namespace '{namespace}'", kind.Component, name, @namespace);
        }
        catch (HttpOperationException ex) when (ex.Response.StatusCode == HttpStatusCode.Conflict) { }
    }

    /// <summary>
    /// Creates the specified Kubernetes namespace, or labels it, so that it is a namespace of Cloud Streams
    /// </summary>
    /// <param name="namespace">The Kubernetes namespace to ensure</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    protected virtual async Task EnsureNamespaceAsync(string @namespace, CancellationToken cancellationToken)
    {
        V1Namespace? existing = null;
        try { existing = await kubernetes.CoreV1.ReadNamespaceAsync(@namespace, cancellationToken: cancellationToken).ConfigureAwait(false); }
        catch (HttpOperationException ex) when (ex.Response.StatusCode == HttpStatusCode.NotFound) { }
        if (existing == null)
        {
            try
            {
                await kubernetes.CoreV1.CreateNamespaceAsync(new V1Namespace() { Metadata = new() { Name = @namespace, Labels = new Dictionary<string, string>() { { store.NamespaceLabel, "true" } } } }, cancellationToken: cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (HttpOperationException ex) when (ex.Response.StatusCode == HttpStatusCode.Conflict) { }
        }
        else if (existing.Metadata.Labels?.TryGetValue(store.NamespaceLabel, out var value) == true && value == "true") return;
        var patch = new JsonObject() { ["metadata"] = new JsonObject() { ["labels"] = new JsonObject() { [store.NamespaceLabel] = "true" } } };
        await kubernetes.CoreV1.PatchNamespaceAsync(new V1Patch(patch, V1Patch.PatchType.MergePatch), @namespace, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

}
