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

namespace CloudStreams.Core.Resources;

/// <summary>
/// Represents an object used to configure the workload that runs a gateway, which the operator deploys in Kubernetes mode (see #279)
/// </summary>
[DataContract]
public record GatewayWorkloadConfiguration
    : WorkloadConfiguration
{

    /// <summary>
    /// Gets/sets a boolean indicating whether or not the gateway serves the management API and the dashboard, for the resources of its own namespace only.
    /// Defaults to false
    /// </summary>
    [DataMember(Order = 9, Name = "managementApi"), JsonPropertyOrder(9), JsonPropertyName("managementApi"), YamlMember(Order = 9, Alias = "managementApi")]
    public virtual bool? ManagementApi { get; set; }

    /// <summary>
    /// Gets/sets an object used to configure the ingress that exposes the gateway's public endpoints, if any
    /// </summary>
    [DataMember(Order = 10, Name = "ingress"), JsonPropertyOrder(10), JsonPropertyName("ingress"), YamlMember(Order = 10, Alias = "ingress")]
    public virtual IngressConfiguration? Ingress { get; set; }

}
