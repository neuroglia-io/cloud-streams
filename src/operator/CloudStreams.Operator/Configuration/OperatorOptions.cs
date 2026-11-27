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

namespace CloudStreams.Operator.Configuration;

/// <summary>
/// Represents the options of the Cloud Streams operator, which deploys gateways and brokers from their resources and validates resources (see #278, #279)
/// </summary>
public class OperatorOptions
{

    /// <summary>
    /// Gets the configuration section the options are bound to
    /// </summary>
    public const string ConfigurationSection = "Operator";

    /// <summary>
    /// Gets the name of the field manager the operator applies Kubernetes objects with
    /// </summary>
    public const string FieldManager = "cloud-streams-operator";

    /// <summary>
    /// Gets/sets the Kubernetes namespace the operator runs in, which holds its lease, its certificate and the connection secret. Defaults to the
    /// 'POD_NAMESPACE' environment variable
    /// </summary>
    public virtual string Namespace { get; set; } = Environment.GetEnvironmentVariable("POD_NAMESPACE") ?? "default";

    /// <summary>
    /// Gets/sets the name of the installation, e.g. the Helm release's full name, which prefixes the names of the cluster-wide objects the operator uses
    /// </summary>
    public virtual string Instance { get; set; } = "cloud-streams";

    /// <summary>
    /// Gets/sets the interval at which every gateway and broker is reconciled again, whether or not it changed
    /// </summary>
    public virtual TimeSpan ResyncInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets/sets the secret of the operator's namespace that holds the connection string of EventStoreDB, which each workload gets a copy of in its own namespace
    /// </summary>
    public virtual SecretKeyReference EventStore { get; set; } = new() { Name = "cloud-streams-connections", Key = "eventstore" };

    /// <summary>
    /// Gets/sets the secret of the operator's namespace that holds the connection string of Redis, which each workload gets a copy of in its own namespace
    /// </summary>
    public virtual SecretKeyReference Redis { get; set; } = new() { Name = "cloud-streams-connections", Key = "redis" };

    /// <summary>
    /// Gets/sets the path of a YAML file of gateways and brokers, e.g. rendered by the Helm chart, that the operator creates when they're missing. It never
    /// updates nor deletes them, so that they may be changed with kubectl or the dashboard
    /// </summary>
    public virtual string? DeclaredResourcesPath { get; set; }

    /// <summary>
    /// Gets/sets the options of the gateways' workloads
    /// </summary>
    public virtual GatewayWorkloadOptions Gateway { get; set; } = new() { Image = "ghcr.io/neuroglia-io/cloud-streams-gateway:latest" };

    /// <summary>
    /// Gets/sets the options of the brokers' workloads
    /// </summary>
    public virtual WorkloadOptions Broker { get; set; } = new() { Image = "ghcr.io/neuroglia-io/cloud-streams-broker:latest" };

    /// <summary>
    /// Gets/sets the options of the shared secrets, which the workloads of some namespaces may read (see #281)
    /// </summary>
    public virtual SharedSecretOptions SharedSecrets { get; set; } = new();

    /// <summary>
    /// Gets/sets the options of the admission webhook the operator serves
    /// </summary>
    public virtual WebhookOptions Webhook { get; set; } = new();

    /// <summary>
    /// Gets/sets the options of the election of the replica that reconciles workloads
    /// </summary>
    public virtual LeaderElectionOptions LeaderElection { get; set; } = new();

    /// <summary>
    /// Gets the name of the ClusterRole the workloads of gateways are bound to, in their own namespace
    /// </summary>
    public virtual string GatewayClusterRole => $"{this.Instance}-gateway";

    /// <summary>
    /// Gets the name of the ClusterRole the workloads of brokers are bound to, in their own namespace
    /// </summary>
    public virtual string BrokerClusterRole => $"{this.Instance}-broker";

    /// <summary>
    /// Gets the name of the ClusterRole that grants the workloads the cluster-wide reads they need: the definitions, schemas and namespaces
    /// </summary>
    public virtual string WorkloadClusterRole => $"{this.Instance}-workload";

    /// <summary>
    /// Gets the name of the ClusterRoleBinding the operator binds the service account of every workload to the <see cref="WorkloadClusterRole"/> with
    /// </summary>
    public virtual string WorkloadClusterRoleBinding => $"{this.Instance}-workload";

    /// <summary>
    /// Validates the options
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the options are invalid</exception>
    public virtual void Validate()
    {
        if (string.IsNullOrWhiteSpace(this.Namespace)) throw new InvalidOperationException("Operator:Namespace is required");
        if (string.IsNullOrWhiteSpace(this.Instance)) throw new InvalidOperationException("Operator:Instance is required");
        if (string.IsNullOrWhiteSpace(this.Gateway.Image)) throw new InvalidOperationException("Operator:Gateway:Image is required");
        if (string.IsNullOrWhiteSpace(this.Broker.Image)) throw new InvalidOperationException("Operator:Broker:Image is required");
        if (this.ResyncInterval <= TimeSpan.Zero) throw new InvalidOperationException("Operator:ResyncInterval must be positive");
    }

}
