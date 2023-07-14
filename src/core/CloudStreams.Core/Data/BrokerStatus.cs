// Copyright © 2023-Present The Cloud Streams Authors
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

namespace CloudStreams.Core.Data;

/// <summary>
/// Represents an object used to describe the status of a cloud event broker
/// </summary>
[DataContract]
public record BrokerStatus
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
    /// Gets/sets the observed generation of the broker's spec the status describes. Divergence between resource and observed generation values should be handled during a reconciliation loop
    /// </summary>
    [DataMember(Order = 3, Name = "observedGeneration"), JsonPropertyOrder(3), JsonPropertyName("observedGeneration"), YamlMember(Order = 3, Alias = "observedGeneration")]
    public virtual ulong ObservedGeneration { get; set; }

    /// <summary>
    /// Gets/sets an object used to describe the status of the broker's cloud event stream
    /// </summary>
    [DataMember(Order = 4, Name = "stream"), JsonPropertyOrder(4), JsonPropertyName("stream"), YamlMember(Order = 4, Alias = "stream")]
    public virtual CloudEventStreamStatus? Stream { get; set; }

}
