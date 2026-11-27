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
/// Represents an object used to describe the status of a gateway
/// </summary>
[DataContract]
public record GatewayStatus
{

    /// <summary>
    /// Gets/sets the gateway's health status
    /// </summary>
    [DataMember(Order = 1, Name = "healthStatus"), JsonPropertyOrder(1), JsonPropertyName("healthStatus"), YamlMember(Order = 1, Alias = "healthStatus")]
    public virtual string? HealthStatus { get; set; }

    /// <summary>
    /// Gets/sets the date and time at which the last gateway health check has been performed
    /// </summary>
    [DataMember(Order = 2, Name = "lastHealthCheckAt"), JsonPropertyOrder(2), JsonPropertyName("lastHealthCheckAt"), YamlMember(Order = 2, Alias = "lastHealthCheckAt")]
    public virtual DateTimeOffset? LastHealthCheckAt { get; set; }

    /// <summary>
    /// Gets/sets the conditions of the gateway's workload, which the operator reports in Kubernetes mode, if any (see #279). See <see cref="ResourceConditionType"/>
    /// </summary>
    [DataMember(Order = 3, Name = "conditions"), JsonPropertyOrder(3), JsonPropertyName("conditions"), YamlMember(Order = 3, Alias = "conditions")]
    public virtual EquatableList<ResourceCondition>? Conditions { get; set; }

    /// <summary>
    /// Gets/sets the number of available replicas of the gateway's workload, which the operator reports in Kubernetes mode, if any
    /// </summary>
    [DataMember(Order = 4, Name = "availableReplicas"), JsonPropertyOrder(4), JsonPropertyName("availableReplicas"), YamlMember(Order = 4, Alias = "availableReplicas")]
    public virtual int? AvailableReplicas { get; set; }

}