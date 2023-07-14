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
/// Represents an object used to describe the result of an health check
/// </summary>
[DataContract]
public record HealthCheckResult
{

    /// <summary>
    /// Initializes a new <see cref="HealthCheckResult"/>
    /// </summary>
    public HealthCheckResult() { }

    /// <summary>
    /// Initializes a new <see cref="HealthCheckResult"/>
    /// </summary>
    /// <param name="name">The name of the described check</param>
    /// <param name="status">The status of the described check. Supported values are 'healthy', 'unhealthy' or 'degraded'</param>
    /// <param name="duration">The duration of the described check</param>
    /// <param name="data">A key/value mapping of the check's data</param>
    public HealthCheckResult(string name, string status, TimeSpan? duration = null, IDictionary<string, object>? data = null)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));
        if (string.IsNullOrWhiteSpace(status)) throw new ArgumentNullException(nameof(status));
        this.Name = name;
        this.Status = status;
        this.Duration = duration;
        this.Data = data;
    }

    /// <summary>
    /// Gets/sets the name of the described check
    /// </summary>
    [Required, JsonRequired, MinLength(3)]
    [DataMember(Order = 1, Name = "name", IsRequired = true), JsonPropertyOrder(1), JsonPropertyName("name"), YamlMember(Order = 1, Alias = "name")]
    public virtual string Name { get; set; } = null!;

    /// <summary>
    /// Gets/sets the status of the described check. Supported values are 'healthy', 'unhealthy' or 'degraded'
    /// </summary>
    [Required, JsonRequired, MinLength(3)]
    [DataMember(Order = 2, Name = "status", IsRequired = true), JsonPropertyOrder(2), JsonPropertyName("status"), YamlMember(Order = 2, Alias = "status")]
    public virtual string Status { get; set; } = null!;

    /// <summary>
    /// Gets/sets the duration of the described check
    /// </summary>
    [DataMember(Order = 3, Name = "duration", IsRequired = true), JsonPropertyOrder(3), JsonPropertyName("duration"), YamlMember(Order = 3, Alias = "duration")]
    public virtual TimeSpan? Duration { get; set; }

    /// <summary>
    /// Gets/sets a key/value mapping of the check's data
    /// </summary>
    [DataMember(Order = 4, Name = "data", IsRequired = true), JsonPropertyOrder(4), JsonPropertyName("data"), YamlMember(Order = 4, Alias = "data")]
    public virtual IDictionary<string, object>? Data { get; set; }

}