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

using System.Text.Json.Nodes;

namespace CloudStreams.Core.Resources;

/// <summary>
/// Represents an object used to configure the workload that runs a gateway or a broker, which the operator deploys in Kubernetes mode (see #279)
/// </summary>
/// <remarks>
/// Whoever may edit the resource may shape its workload: the image, the command, the environment and the service account are therefore not configurable here,
/// but only by the operator's configuration, so that editing a resource never grants the rights to run arbitrary code with the workload's identity
/// </remarks>
[DataContract]
public record WorkloadConfiguration
{

    /// <summary>
    /// Gets/sets the number of replicas to run. Defaults to the operator's configuration
    /// </summary>
    [Range(0, 100)]
    [DataMember(Order = 1, Name = "replicas"), JsonPropertyOrder(1), JsonPropertyName("replicas"), YamlMember(Order = 1, Alias = "replicas")]
    public virtual int? Replicas { get; set; }

    /// <summary>
    /// Gets/sets the compute resources each replica requests and is limited to, if any
    /// </summary>
    [DataMember(Order = 2, Name = "resources"), JsonPropertyOrder(2), JsonPropertyName("resources"), YamlMember(Order = 2, Alias = "resources")]
    public virtual WorkloadResourceRequirements? Resources { get; set; }

    /// <summary>
    /// Gets/sets the labels of the nodes the replicas must run on, if any
    /// </summary>
    [DataMember(Order = 3, Name = "nodeSelector"), JsonPropertyOrder(3), JsonPropertyName("nodeSelector"), YamlMember(Order = 3, Alias = "nodeSelector")]
    public virtual IDictionary<string, string>? NodeSelector { get; set; }

    /// <summary>
    /// Gets/sets the Kubernetes tolerations of the replicas, if any
    /// </summary>
    [DataMember(Order = 4, Name = "tolerations"), JsonPropertyOrder(4), JsonPropertyName("tolerations"), YamlMember(Order = 4, Alias = "tolerations")]
    public virtual EquatableList<JsonObject>? Tolerations { get; set; }

    /// <summary>
    /// Gets/sets the Kubernetes affinity of the replicas, if any
    /// </summary>
    [DataMember(Order = 5, Name = "affinity"), JsonPropertyOrder(5), JsonPropertyName("affinity"), YamlMember(Order = 5, Alias = "affinity")]
    public virtual JsonObject? Affinity { get; set; }

    /// <summary>
    /// Gets/sets the Kubernetes topology spread constraints of the replicas, if any
    /// </summary>
    [DataMember(Order = 6, Name = "topologySpreadConstraints"), JsonPropertyOrder(6), JsonPropertyName("topologySpreadConstraints"), YamlMember(Order = 6, Alias = "topologySpreadConstraints")]
    public virtual EquatableList<JsonObject>? TopologySpreadConstraints { get; set; }

    /// <summary>
    /// Gets/sets the labels added to the replicas, if any. They can't override the labels the operator selects the replicas by
    /// </summary>
    [DataMember(Order = 7, Name = "podLabels"), JsonPropertyOrder(7), JsonPropertyName("podLabels"), YamlMember(Order = 7, Alias = "podLabels")]
    public virtual IDictionary<string, string>? PodLabels { get; set; }

    /// <summary>
    /// Gets/sets the annotations added to the replicas, if any
    /// </summary>
    [DataMember(Order = 8, Name = "podAnnotations"), JsonPropertyOrder(8), JsonPropertyName("podAnnotations"), YamlMember(Order = 8, Alias = "podAnnotations")]
    public virtual IDictionary<string, string>? PodAnnotations { get; set; }

}
