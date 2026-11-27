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
/// Represents an aspect of the state of a resource, as Kubernetes conditions do, e.g. whether the workload of a gateway is ready
/// </summary>
[DataContract]
public record ResourceCondition
{

    /// <summary>
    /// Initializes a new <see cref="ResourceCondition"/>
    /// </summary>
    public ResourceCondition() { }

    /// <summary>
    /// Initializes a new <see cref="ResourceCondition"/>
    /// </summary>
    /// <param name="type">The condition's type. See <see cref="ResourceConditionType"/></param>
    /// <param name="status">The condition's status. See <see cref="ResourceConditionStatus"/></param>
    /// <param name="reason">A machine-readable, CamelCase reason for the condition's last transition</param>
    /// <param name="message">A human-readable message about the condition, if any</param>
    /// <param name="observedGeneration">The generation of the resource the condition was set for</param>
    /// <param name="lastTransitionTime">The date and time at which the condition's status last changed</param>
    public ResourceCondition(string type, string status, string reason, string? message, ulong observedGeneration, DateTimeOffset lastTransitionTime)
    {
        this.Type = type;
        this.Status = status;
        this.Reason = reason;
        this.Message = message;
        this.ObservedGeneration = observedGeneration;
        this.LastTransitionTime = lastTransitionTime;
    }

    /// <summary>
    /// Gets/sets the condition's type. See <see cref="ResourceConditionType"/>
    /// </summary>
    [Required]
    [DataMember(Order = 1, Name = "type"), JsonPropertyOrder(1), JsonPropertyName("type"), YamlMember(Order = 1, Alias = "type")]
    public virtual string Type { get; set; } = null!;

    /// <summary>
    /// Gets/sets the condition's status: 'True', 'False' or 'Unknown'. See <see cref="ResourceConditionStatus"/>
    /// </summary>
    [Required]
    [DataMember(Order = 2, Name = "status"), JsonPropertyOrder(2), JsonPropertyName("status"), YamlMember(Order = 2, Alias = "status")]
    public virtual string Status { get; set; } = null!;

    /// <summary>
    /// Gets/sets a machine-readable, CamelCase reason for the condition's last transition
    /// </summary>
    [DataMember(Order = 3, Name = "reason"), JsonPropertyOrder(3), JsonPropertyName("reason"), YamlMember(Order = 3, Alias = "reason")]
    public virtual string? Reason { get; set; }

    /// <summary>
    /// Gets/sets a human-readable message about the condition, if any
    /// </summary>
    [DataMember(Order = 4, Name = "message"), JsonPropertyOrder(4), JsonPropertyName("message"), YamlMember(Order = 4, Alias = "message")]
    public virtual string? Message { get; set; }

    /// <summary>
    /// Gets/sets the generation of the resource the condition was set for: when it is lower than the resource's, the condition is out of date
    /// </summary>
    [DataMember(Order = 5, Name = "observedGeneration"), JsonPropertyOrder(5), JsonPropertyName("observedGeneration"), YamlMember(Order = 5, Alias = "observedGeneration")]
    public virtual ulong ObservedGeneration { get; set; }

    /// <summary>
    /// Gets/sets the date and time at which the condition's status last changed
    /// </summary>
    [DataMember(Order = 6, Name = "lastTransitionTime"), JsonPropertyOrder(6), JsonPropertyName("lastTransitionTime"), YamlMember(Order = 6, Alias = "lastTransitionTime")]
    public virtual DateTimeOffset LastTransitionTime { get; set; }

}
