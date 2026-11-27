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

namespace CloudStreams.Operator.Services;

/// <summary>
/// Represents the Kubernetes objects that run the workload of a gateway or a broker, all in the resource's namespace and owned by it
/// </summary>
public record Workload
{

    /// <summary>
    /// Gets the name the workload's objects share
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the service account the workload runs with
    /// </summary>
    public required V1ServiceAccount ServiceAccount { get; init; }

    /// <summary>
    /// Gets the role binding that grants the workload's service account the rights of its component, in its namespace only
    /// </summary>
    public required V1RoleBinding RoleBinding { get; init; }

    /// <summary>
    /// Gets the copy of the connection strings the workload reads
    /// </summary>
    public required V1Secret Connections { get; init; }

    /// <summary>
    /// Gets the deployment that runs the workload's replicas
    /// </summary>
    public required V1Deployment Deployment { get; init; }

    /// <summary>
    /// Gets the service that exposes the workload
    /// </summary>
    public required V1Service Service { get; init; }

    /// <summary>
    /// Gets the pod disruption budget of the workload, if any
    /// </summary>
    public V1PodDisruptionBudget? PodDisruptionBudget { get; init; }

    /// <summary>
    /// Gets the ingress that exposes the public endpoints of a gateway, if any
    /// </summary>
    public V1Ingress? Ingress { get; init; }

}
