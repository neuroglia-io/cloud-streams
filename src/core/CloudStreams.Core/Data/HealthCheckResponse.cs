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
/// Represents an object used to describe an health check's response
/// </summary>
[DataContract]
public record HealthCheckResponse
{

    /// <summary>
    /// Initializes a new <see cref="HealthCheckResponse"/>
    /// </summary>
    public HealthCheckResponse() { }

    /// <summary>
    /// Initializes a new <see cref="HealthCheckResponse"/>
    /// </summary>
    /// <param name="status">The service's status. Supported values are 'healthy', 'unhealthy' or 'degraded'</param>
    /// <param name="checks">A list containing objects that describe the checks that have been performed</param>
    public HealthCheckResponse(string status, IEnumerable<HealthCheckResult>? checks = null)
    {
        if(string.IsNullOrWhiteSpace(status)) throw new ArgumentNullException(nameof(status));
        this.Status = status;
        this.Checks = checks == null ? null : new(checks);
    }

    /// <summary>
    /// Gets/sets the service's status. Supported values are 'healthy', 'unhealthy' or 'degraded'
    /// </summary>
    [Required, JsonRequired, MinLength(3)]
    [DataMember(Order = 1, Name = "status", IsRequired = true), JsonPropertyOrder(1), JsonPropertyName("status"), YamlMember(Order = 1, Alias = "status")]
    public virtual string Status { get; set; } = null!;

    /// <summary>
    /// Gets/sets a list containing objects that describe the checks that have been performed
    /// </summary>
    [DataMember(Order = 2, Name = "checks", IsRequired = true), JsonPropertyOrder(2), JsonPropertyName("checks"), YamlMember(Order = 2, Alias = "checks")]
    public virtual EquatableList<HealthCheckResult>? Checks { get; set; }

}
