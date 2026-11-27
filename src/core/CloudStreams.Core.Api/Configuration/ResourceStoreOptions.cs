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

namespace CloudStreams.Core.Api.Configuration;

/// <summary>
/// Represents the options used to configure where the resources of Cloud Streams are stored (see #275)
/// </summary>
public class ResourceStoreOptions
{

    /// <summary>
    /// Gets the path of the configuration section the options are bound to
    /// </summary>
    public const string ConfigurationSection = "CloudStreams:Resources";

    /// <summary>
    /// Gets/sets the store of the resources: <see cref="ResourceStoreType.Redis"/>, the default, or <see cref="ResourceStoreType.Kubernetes"/>, where resources are
    /// Kubernetes custom resources
    /// </summary>
    public virtual string Store { get; set; } = ResourceStoreType.Redis;

    /// <summary>
    /// Gets/sets the options of the Kubernetes store
    /// </summary>
    public virtual KubernetesResourceStoreOptions Kubernetes { get; set; } = new();

    /// <summary>
    /// Gets a boolean indicating whether resources are stored in Kubernetes
    /// </summary>
    public virtual bool UsesKubernetes => string.Equals(this.Store?.Trim(), ResourceStoreType.Kubernetes, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Validates the options
    /// </summary>
    /// <exception cref="InvalidOperationException">The options are invalid</exception>
    public virtual void Validate()
    {
        if (!string.Equals(this.Store?.Trim(), ResourceStoreType.Redis, StringComparison.OrdinalIgnoreCase) && !this.UsesKubernetes)
            throw new InvalidOperationException($"'{ConfigurationSection}:Store' must be '{ResourceStoreType.Redis}' or '{ResourceStoreType.Kubernetes}', not '{this.Store}'");
        if (this.UsesKubernetes && !Neuroglia.Data.Infrastructure.ResourceOriented.ObjectNamingConvention.Current.IsValidResourceName(this.Kubernetes.DefaultNamespace ?? string.Empty))
            throw new InvalidOperationException($"'{ConfigurationSection}:Kubernetes:DefaultNamespace' must be the name of a Kubernetes namespace, not '{this.Kubernetes.DefaultNamespace}'");
        var invalid = this.Kubernetes.Namespaces?.Where(n => !Application.Validation.CloudStreamsResourceValidator.IsValidNamespaceName(n?.Trim())).ToList();
        if (this.UsesKubernetes && invalid?.Count > 0)
            throw new InvalidOperationException($"'{ConfigurationSection}:Kubernetes:Namespaces' must only list namespace names, which {string.Join(", ", invalid.Select(n => $"'{n}'"))} are not");
    }

}

/// <summary>
/// Enumerates the stores the resources of Cloud Streams can be kept in
/// </summary>
public static class ResourceStoreType
{

    /// <summary>
    /// Indicates that resources are stored in Redis, which Cloud Streams runs with anywhere
    /// </summary>
    public const string Redis = "redis";

    /// <summary>
    /// Indicates that resources are Kubernetes custom resources, managed with kubectl and Kubernetes RBAC too (see #274)
    /// </summary>
    public const string Kubernetes = "kubernetes";

}

/// <summary>
/// Represents the options of the Kubernetes store of resources (see #275, #277)
/// </summary>
public class KubernetesResourceStoreOptions
{

    /// <summary>
    /// Gets/sets the Kubernetes namespace that the default namespace of Cloud Streams is. Defaults to <c>default</c>
    /// </summary>
    /// <remarks>Other namespaces of Cloud Streams are the Kubernetes namespaces of the same name</remarks>
    public virtual string DefaultNamespace { get; set; } = "default";

    /// <summary>
    /// Gets/sets the label that makes a Kubernetes namespace a namespace of Cloud Streams. Defaults to <c>cloud-streams.io/enabled</c>, with the value <c>true</c>
    /// </summary>
    public virtual string NamespaceLabel { get; set; } = "cloud-streams.io/enabled";

    /// <summary>
    /// Gets/sets a boolean indicating whether creating a namespace of Cloud Streams creates the Kubernetes namespace if it does not exist, which requires the right to
    /// create namespaces. Otherwise, only existing Kubernetes namespaces can be labelled. Defaults to <see langword="true"/>
    /// </summary>
    public virtual bool CreateNamespaces { get; set; } = true;

    /// <summary>
    /// Gets/sets the namespaces of Cloud Streams the process serves, if not all of them. Defaults to every namespace
    /// </summary>
    /// <remarks>
    /// The process then only reads and writes the resources of those namespaces, and refuses the others with a <c>403 Forbidden</c>. It lists and watches them one
    /// namespace at a time, so that it only needs rights in those Kubernetes namespaces, e.g. those of a RoleBinding in each
    /// </remarks>
    public virtual List<string>? Namespaces { get; set; }

    /// <summary>
    /// Gets/sets the path of the kubeconfig file used outside of a cluster, if any. In a cluster, the pod's service account is used
    /// </summary>
    public virtual string? KubeConfigPath { get; set; }

    /// <summary>
    /// Gets/sets the context of the kubeconfig file to use outside of a cluster, if not its current one
    /// </summary>
    public virtual string? Context { get; set; }

    /// <summary>
    /// Gets/sets the maximum amount of resources read per request when listing them. Defaults to 500
    /// </summary>
    public virtual int PageSize { get; set; } = 500;

    /// <summary>
    /// Gets/sets how long the API server keeps a watch open before closing it, after which it is resumed where it stopped. The API server chooses if not set
    /// </summary>
    public virtual TimeSpan? WatchTimeout { get; set; }

}
