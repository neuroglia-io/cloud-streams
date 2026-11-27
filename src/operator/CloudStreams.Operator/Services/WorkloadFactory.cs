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
using Neuroglia.Data.Infrastructure.ResourceOriented;
using System.Security.Cryptography;
using System.Text;

namespace CloudStreams.Operator.Services;

/// <summary>
/// Builds the Kubernetes objects that run the workload of a gateway or a broker (see #279)
/// </summary>
/// <param name="options">The options of the operator</param>
/// <param name="store">The options of the Kubernetes store of resources, which tell which Kubernetes namespace the 'default' namespace of Cloud Streams is</param>
public class WorkloadFactory(OperatorOptions options, KubernetesResourceStoreOptions store)
{

    /// <summary>
    /// Gets the label that tells the gateway or the broker whose workload an object belongs to, e.g. 'cloud-streams.io/gateway'
    /// </summary>
    public const string ResourceLabelPrefix = "cloud-streams.io/";

    /// <summary>
    /// Gets the label that tells the installation whose operator manages an object
    /// </summary>
    public const string OperatorLabel = "cloud-streams.io/operator";

    /// <summary>
    /// Gets the value of the 'app.kubernetes.io/managed-by' label of the objects the operator manages
    /// </summary>
    public const string ManagedBy = "cloud-streams-operator";

    /// <summary>
    /// Gets the name of the port the workloads serve HTTP on
    /// </summary>
    public const string HttpPortName = "http";

    /// <summary>
    /// Gets the name of the objects of the workload of the specified resource, e.g. 'orders-gateway', at most 63 characters long
    /// </summary>
    /// <param name="kind">The kind of the resource</param>
    /// <param name="name">The name of the resource</param>
    /// <returns>The name of the workload's objects</returns>
    public static string GetObjectName(WorkloadKind kind, string name)
    {
        var objectName = $"{name}-{kind.Component}";
        if (objectName.Length <= 63) return objectName;
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(objectName)))[..8].ToLowerInvariant();
        return $"{objectName[..(63 - hash.Length - 1)].TrimEnd('-')}-{hash}";
    }

    /// <summary>
    /// Gets the labels the replicas of the workload of the specified resource are selected by
    /// </summary>
    /// <param name="kind">The kind of the resource</param>
    /// <param name="name">The name of the resource</param>
    /// <returns>The selector labels</returns>
    public static Dictionary<string, string> GetSelectorLabels(WorkloadKind kind, string name) => new()
    {
        { "app.kubernetes.io/name", $"cloud-streams-{kind.Component}" },
        { "app.kubernetes.io/component", kind.Component },
        { ResourceLabelPrefix + kind.Component, name }
    };

    /// <summary>
    /// Gets the labels of the objects of the workload of the specified resource
    /// </summary>
    /// <param name="kind">The kind of the resource</param>
    /// <param name="name">The name of the resource</param>
    /// <returns>The labels of the workload's objects</returns>
    public virtual Dictionary<string, string> GetLabels(WorkloadKind kind, string name)
    {
        var labels = GetSelectorLabels(kind, name);
        labels["app.kubernetes.io/part-of"] = "cloud-streams";
        labels["app.kubernetes.io/managed-by"] = ManagedBy;
        labels[OperatorLabel] = options.Instance;
        return labels;
    }

    /// <summary>
    /// Gets the namespace of Cloud Streams the specified Kubernetes namespace is
    /// </summary>
    /// <param name="kubernetesNamespace">The Kubernetes namespace</param>
    /// <returns>The namespace of Cloud Streams</returns>
    public virtual string GetCloudStreamsNamespace(string kubernetesNamespace) => kubernetesNamespace == store.DefaultNamespace ? Namespace.DefaultNamespaceName : kubernetesNamespace;

    /// <summary>
    /// Builds the objects of the workload of the specified resource
    /// </summary>
    /// <param name="resource">The gateway or the broker to build the workload of</param>
    /// <param name="connections">The connection strings the workload reads, keyed by name: 'eventstore' and 'redis'</param>
    /// <returns>A new <see cref="Workload"/></returns>
    public virtual Workload Create(ManagedResource resource, IDictionary<string, byte[]> connections)
    {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(connections);
        var kind = resource.Kind;
        var defaults = kind == WorkloadKind.Gateway ? options.Gateway : options.Broker;
        var workload = resource.Workload ?? new WorkloadConfiguration();
        var name = GetObjectName(kind, resource.Name);
        var connectionsName = $"{name}-connections";
        var labels = this.GetLabels(kind, resource.Name);
        var selector = GetSelectorLabels(kind, resource.Name);
        V1ObjectMeta Metadata(string objectName) => new()
        {
            Name = objectName,
            NamespaceProperty = resource.Namespace,
            Labels = new Dictionary<string, string>(labels),
            OwnerReferences = [new V1OwnerReference($"{CloudStreamsDefaults.ResourceGroup}/v1", kind.Kind, resource.Name, resource.Uid, blockOwnerDeletion: true, controller: true)]
        };

        var podLabels = new Dictionary<string, string>(workload.PodLabels ?? new Dictionary<string, string>());
        foreach (var label in selector) podLabels[label.Key] = label.Value;
        var podAnnotations = new Dictionary<string, string>(workload.PodAnnotations ?? new Dictionary<string, string>())
        {
            // the replicas restart when the connection strings change
            ["checksum/connections"] = Convert.ToHexString(SHA256.HashData(connections.OrderBy(c => c.Key).SelectMany(c => Encoding.UTF8.GetBytes(c.Key).Concat(c.Value)).ToArray())).ToLowerInvariant()
        };
        var gatewayWorkload = workload as GatewayWorkloadConfiguration;
        var container = new V1Container()
        {
            Name = kind.Component,
            Image = defaults.Image,
            ImagePullPolicy = defaults.ImagePullPolicy,
            Ports = [new V1ContainerPort(defaults.Port, name: HttpPortName)],
            Env = this.CreateEnvironment(resource, defaults, connectionsName, gatewayWorkload?.ManagementApi == true),
            Resources = ToKubernetes(workload.Resources ?? defaults.Resources),
            SecurityContext = defaults.SecurityContext,
            LivenessProbe = new() { HttpGet = new() { Path = "/healthz/live", Port = HttpPortName }, InitialDelaySeconds = 10, PeriodSeconds = 10, TimeoutSeconds = 5, FailureThreshold = 3 },
            ReadinessProbe = new() { HttpGet = new() { Path = "/healthz", Port = HttpPortName }, InitialDelaySeconds = 5, PeriodSeconds = 10, TimeoutSeconds = 5, FailureThreshold = 3 }
        };
        var deployment = new V1Deployment()
        {
            ApiVersion = "apps/v1",
            Kind = "Deployment",
            Metadata = Metadata(name),
            Spec = new()
            {
                Replicas = workload.Replicas ?? defaults.Replicas,
                Selector = new() { MatchLabels = selector },
                Template = new()
                {
                    Metadata = new() { Labels = podLabels, Annotations = podAnnotations },
                    Spec = new()
                    {
                        ServiceAccountName = name,
                        AutomountServiceAccountToken = true,
                        // the variables Kubernetes would inject for every service of the namespace are not used, and some applications mistake them for their own settings
                        EnableServiceLinks = false,
                        SecurityContext = defaults.PodSecurityContext,
                        ImagePullSecrets = defaults.ImagePullSecrets.Count < 1 ? null : defaults.ImagePullSecrets.Select(s => new V1LocalObjectReference(s)).ToList(),
                        Containers = [container],
                        NodeSelector = workload.NodeSelector?.ToDictionary(),
                        Tolerations = workload.Tolerations?.Select(t => KubernetesJson.Deserialize<V1Toleration>(t.ToJsonString())).ToList(),
                        Affinity = workload.Affinity == null ? null : KubernetesJson.Deserialize<V1Affinity>(workload.Affinity.ToJsonString()),
                        TopologySpreadConstraints = workload.TopologySpreadConstraints?.Select(t => KubernetesJson.Deserialize<V1TopologySpreadConstraint>(t.ToJsonString())).ToList()
                    }
                }
            }
        };
        var service = new V1Service()
        {
            ApiVersion = "v1",
            Kind = "Service",
            Metadata = Metadata(name),
            Spec = new()
            {
                Type = "ClusterIP",
                Selector = selector,
                Ports = [new V1ServicePort(80, name: HttpPortName, targetPort: HttpPortName)]
            }
        };
        var pdb = defaults.PodDisruptionBudget ? new V1PodDisruptionBudget()
        {
            ApiVersion = "policy/v1",
            Kind = "PodDisruptionBudget",
            Metadata = Metadata(name),
            Spec = new() { MaxUnavailable = defaults.MaxUnavailable, Selector = new() { MatchLabels = selector } }
        } : null;
        V1Ingress? ingress = null;
        if (gatewayWorkload?.Ingress is IngressConfiguration ingressConfiguration && ingressConfiguration.Hosts.Count > 0)
        {
            var metadata = Metadata(name);
            if (options.Gateway.IngressAnnotations.Count > 0) metadata.Annotations = new Dictionary<string, string>(options.Gateway.IngressAnnotations);
            ingress = new()
            {
                ApiVersion = "networking.k8s.io/v1",
                Kind = "Ingress",
                Metadata = metadata,
                Spec = new()
                {
                    IngressClassName = ingressConfiguration.ClassName ?? options.Gateway.IngressClassName,
                    Tls = string.IsNullOrWhiteSpace(ingressConfiguration.TlsSecretName) ? null : [new V1IngressTLS([.. ingressConfiguration.Hosts], ingressConfiguration.TlsSecretName)],
                    Rules = ingressConfiguration.Hosts.Select(host => new V1IngressRule(host, new V1HTTPIngressRuleValue(
                    [
                        new V1HTTPIngressPath(new V1IngressBackend(service: new V1IngressServiceBackend(name, new V1ServiceBackendPort(name: HttpPortName))), "Prefix", "/")
                    ]))).ToList()
                }
            };
        }
        return new()
        {
            Name = name,
            ServiceAccount = new()
            {
                ApiVersion = "v1",
                Kind = "ServiceAccount",
                Metadata = Metadata(name),
                AutomountServiceAccountToken = false
            },
            RoleBinding = new()
            {
                ApiVersion = "rbac.authorization.k8s.io/v1",
                Kind = "RoleBinding",
                Metadata = Metadata(name),
                RoleRef = new("rbac.authorization.k8s.io", "ClusterRole", kind == WorkloadKind.Gateway ? options.GatewayClusterRole : options.BrokerClusterRole),
                Subjects = [new Rbacv1Subject("ServiceAccount", name, namespaceProperty: resource.Namespace)]
            },
            Connections = new()
            {
                ApiVersion = "v1",
                Kind = "Secret",
                Metadata = Metadata(connectionsName),
                Type = "Opaque",
                Data = connections.ToDictionary(c => c.Key, c => c.Value)
            },
            Deployment = deployment,
            Service = service,
            PodDisruptionBudget = pdb,
            Ingress = ingress
        };
    }

    /// <summary>
    /// Creates the environment variables of the workload of the specified resource
    /// </summary>
    /// <param name="resource">The gateway or the broker to create the environment variables of</param>
    /// <param name="defaults">The options of the workloads of the resource's kind</param>
    /// <param name="connectionsName">The name of the secret that holds the connection strings</param>
    /// <param name="managementApi">A boolean indicating whether or not a gateway serves the management API</param>
    /// <returns>The environment variables of the workload</returns>
    protected virtual List<V1EnvVar> CreateEnvironment(ManagedResource resource, WorkloadOptions defaults, string connectionsName, bool managementApi)
    {
        var @namespace = this.GetCloudStreamsNamespace(resource.Namespace);
        var env = new List<V1EnvVar>()
        {
            new("ASPNETCORE_HTTP_PORTS", defaults.Port.ToString()),
            new(resource.Kind.EnvironmentVariable("NAME"), resource.Name),
            new(resource.Kind.EnvironmentVariable("NAMESPACE"), @namespace),
            new("ConnectionStrings__eventstore", valueFrom: new() { SecretKeyRef = new("eventstore", connectionsName) }),
            new("ConnectionStrings__redis", valueFrom: new() { SecretKeyRef = new("redis", connectionsName) }),
            // the workload stores resources as custom resources, and only serves the namespace of its resource, where it is granted rights (see #282)
            new("CloudStreams__Resources__Store", ResourceStoreType.Kubernetes),
            new("CloudStreams__Resources__Kubernetes__DefaultNamespace", store.DefaultNamespace),
            new("CloudStreams__Resources__Kubernetes__Namespaces__0", @namespace),
            new("CloudStreams__Resources__Kubernetes__CreateNamespaces", "false"),
            // the 'kubernetes' secret provider reads the secrets of the workload's namespace, and the shared ones from the operator's namespace (see #281)
            new("CloudStreams__Secrets__Kubernetes__NamespacedSecrets", "true"),
            new("CloudStreams__Secrets__Kubernetes__DefaultNamespace", store.DefaultNamespace),
            new("CloudStreams__Secrets__Kubernetes__Namespace", options.Namespace),
            // the namespace of the leases and of the data-protection keys, and the identity of the replica (see #280)
            new("POD_NAMESPACE", valueFrom: new() { FieldRef = new("metadata.namespace") }),
            new("POD_NAME", valueFrom: new() { FieldRef = new("metadata.name") })
        };
        if (resource.Kind == WorkloadKind.Gateway) env.Add(new("ManagementApi__Enabled", managementApi ? "true" : "false"));
        var reserved = env.Select(e => e.Name).ToHashSet();
        env.AddRange(defaults.Env.Where(e => !reserved.Contains(e.Name)));
        return env;
    }

    /// <summary>
    /// Converts the specified <see cref="WorkloadResourceRequirements"/> into Kubernetes resource requirements
    /// </summary>
    /// <param name="resources">The requirements to convert</param>
    /// <returns>The converted requirements, if any</returns>
    protected static V1ResourceRequirements? ToKubernetes(WorkloadResourceRequirements? resources)
    {
        if (resources == null || ((resources.Requests?.Count ?? 0) < 1 && (resources.Limits?.Count ?? 0) < 1)) return null;
        return new()
        {
            Requests = resources.Requests?.ToDictionary(r => r.Key, r => new ResourceQuantity(r.Value)),
            Limits = resources.Limits?.ToDictionary(r => r.Key, r => new ResourceQuantity(r.Value))
        };
    }

}
