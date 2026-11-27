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
using k8s.Models;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace CloudStreams.Core.Api.Services;

/// <summary>
/// Represents the <see cref="IHealthMonitorLeaseManager"/> that stores leases as Kubernetes <c>Lease</c>s, in the Kubernetes namespace of the monitored resource, in
/// Kubernetes mode (see #280)
/// </summary>
/// <remarks>
/// The lease lives in the namespace of the monitored resource, so that the API, which serves every namespace, and the gateways of that namespace compete for the same lease
/// </remarks>
/// <param name="kubernetes">The service used to interact with the Kubernetes API</param>
/// <param name="options">The options of the Kubernetes store of resources</param>
/// <param name="timeProvider">The service used to get the current time</param>
public partial class KubernetesHealthMonitorLeaseManager(IKubernetes kubernetes, KubernetesResourceStoreOptions options, TimeProvider timeProvider)
    : IHealthMonitorLeaseManager
{

    /// <summary>
    /// Gets the prefix of the names of the leases
    /// </summary>
    public const string NamePrefix = "cloud-streams-health-";

    /// <inheritdoc/>
    public string OwnerId { get; } = $"{Environment.GetEnvironmentVariable("POD_NAME") ?? Environment.MachineName}/{Guid.NewGuid():N}";

    /// <summary>
    /// Gets the name of the lease of the specified key, a valid name of a Kubernetes object
    /// </summary>
    /// <param name="key">The key of the lease</param>
    /// <returns>The name of the lease</returns>
    public static string GetLeaseName(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var name = NamePrefix + InvalidCharacters().Replace(key.ToLowerInvariant(), "-").Trim('-', '.');
        if (name.Length <= 253) return name;
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..16].ToLowerInvariant();
        return $"{name[..(253 - hash.Length - 1)].TrimEnd('-', '.')}-{hash}";
    }

    /// <inheritdoc/>
    public virtual async Task<bool> TryAcquireAsync(string key, string? @namespace, TimeSpan duration, CancellationToken cancellationToken = default)
    {
        var name = GetLeaseName(key);
        var kubernetesNamespace = options.GetKubernetesNamespace(@namespace);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var seconds = (int)Math.Max(1, Math.Ceiling(duration.TotalSeconds));
        V1Lease? lease = null;
        try { lease = await kubernetes.CoordinationV1.ReadNamespacedLeaseAsync(name, kubernetesNamespace, cancellationToken: cancellationToken).ConfigureAwait(false); }
        catch (HttpOperationException ex) when (ex.Response.StatusCode == HttpStatusCode.NotFound) { }
        if (lease == null)
        {
            var created = new V1Lease()
            {
                Metadata = new() { Name = name, NamespaceProperty = kubernetesNamespace, Labels = new Dictionary<string, string>() { { "app.kubernetes.io/managed-by", "cloud-streams" } } },
                Spec = new() { HolderIdentity = this.OwnerId, LeaseDurationSeconds = seconds, AcquireTime = now, RenewTime = now }
            };
            try
            {
                await kubernetes.CoordinationV1.CreateNamespacedLeaseAsync(created, kubernetesNamespace, cancellationToken: cancellationToken).ConfigureAwait(false);
                return true;
            }
            catch (HttpOperationException ex) when (ex.Response.StatusCode == HttpStatusCode.Conflict) { return false; /* acquired by another process in the meantime */ }
        }
        var held = lease.Spec?.HolderIdentity == this.OwnerId;
        var expired = lease.Spec?.RenewTime is not DateTime renewed || renewed.ToUniversalTime().AddSeconds(lease.Spec.LeaseDurationSeconds ?? 0) <= now;
        if (!held && !expired && !string.IsNullOrWhiteSpace(lease.Spec?.HolderIdentity)) return false;
        lease.Spec ??= new();
        if (!held) lease.Spec.AcquireTime = now;
        lease.Spec.HolderIdentity = this.OwnerId;
        lease.Spec.LeaseDurationSeconds = seconds;
        lease.Spec.RenewTime = now;
        try
        {
            // the resource version the lease was read with guards against another process that acquires it concurrently
            await kubernetes.CoordinationV1.ReplaceNamespacedLeaseAsync(lease, name, kubernetesNamespace, cancellationToken: cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (HttpOperationException ex) when (ex.Response.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.NotFound) { return false; }
    }

    /// <inheritdoc/>
    public virtual async Task ReleaseAsync(string key, string? @namespace, CancellationToken cancellationToken = default)
    {
        var name = GetLeaseName(key);
        var kubernetesNamespace = options.GetKubernetesNamespace(@namespace);
        V1Lease lease;
        try { lease = await kubernetes.CoordinationV1.ReadNamespacedLeaseAsync(name, kubernetesNamespace, cancellationToken: cancellationToken).ConfigureAwait(false); }
        catch (HttpOperationException ex) when (ex.Response.StatusCode == HttpStatusCode.NotFound) { return; }
        if (lease.Spec?.HolderIdentity != this.OwnerId) return;
        try
        {
            await kubernetes.CoordinationV1.DeleteNamespacedLeaseAsync(name, kubernetesNamespace, new V1DeleteOptions() { Preconditions = new() { ResourceVersion = lease.Metadata.ResourceVersion } }, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (HttpOperationException ex) when (ex.Response.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.NotFound) { /* renewed or released by another process in the meantime */ }
    }

    [GeneratedRegex("[^a-z0-9.-]+")]
    private static partial Regex InvalidCharacters();

}
