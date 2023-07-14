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

}
