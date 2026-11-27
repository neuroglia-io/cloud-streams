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
/// Describes a kind of resource whose workloads the operator deploys: gateways or brokers
/// </summary>
/// <param name="Component">The name of the component, e.g. 'gateway', which names its objects, labels and environment variables</param>
/// <param name="Kind">The Kubernetes kind of the resource, e.g. 'Gateway'</param>
/// <param name="Plural">The plural name of the resource, e.g. 'gateways'</param>
public record WorkloadKind(string Component, string Kind, string Plural)
{

    /// <summary>
    /// Gets the <see cref="WorkloadKind"/> of gateways
    /// </summary>
    public static WorkloadKind Gateway { get; } = new("gateway", "Gateway", "gateways");

    /// <summary>
    /// Gets the <see cref="WorkloadKind"/> of brokers
    /// </summary>
    public static WorkloadKind Broker { get; } = new("broker", "Broker", "brokers");

    /// <summary>
    /// Gets the kinds of resources whose workloads the operator deploys
    /// </summary>
    public static IReadOnlyList<WorkloadKind> All { get; } = [Gateway, Broker];

    /// <summary>
    /// Gets the <see cref="WorkloadKind"/> of the specified component
    /// </summary>
    /// <param name="component">The name of the component, e.g. 'gateway'</param>
    /// <returns>The <see cref="WorkloadKind"/> of the component, if any</returns>
    public static WorkloadKind? FromComponent(string? component) => All.FirstOrDefault(k => k.Component == component);

    /// <summary>
    /// Gets the name of the environment variable that tells the workload the name or the namespace of the resource it serves, e.g. 'CLOUDSTREAMS_GATEWAY_NAME'
    /// </summary>
    /// <param name="suffix">The suffix of the variable: 'NAME' or 'NAMESPACE'</param>
    /// <returns>The name of the environment variable</returns>
    public string EnvironmentVariable(string suffix) => $"CLOUDSTREAMS_{this.Component.ToUpperInvariant()}_{suffix}";

}
