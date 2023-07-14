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
/// Represents an object used to configure a service
/// </summary>
[DataContract]
public record ServiceConfiguration
{

    /// <summary>
    /// Initializes a new <see cref="ServiceConfiguration"/>
    /// </summary>
    public ServiceConfiguration() { }

    /// <summary>
    /// Initializes a new <see cref="ServiceConfiguration"/>
    /// </summary>
    /// <param name="uri">The base uri of the configured service</param>
    /// <param name="healthChecks">An object used to configure the service's health checks, if any</param>
    public ServiceConfiguration(Uri uri, ServiceHealthCheckConfiguration? healthChecks = null)
    {
        this.Uri = uri ?? throw new ArgumentNullException(nameof(uri));
        this.HealthChecks = healthChecks;
    }

    /// <summary>
    /// Gets/sets the base uri of the configured service
    /// </summary>
    [DataMember(Order = 1, Name = "uri"), JsonPropertyOrder(1), JsonPropertyName("uri"), YamlMember(Order = 1, Alias = "uri")]
    public virtual Uri Uri { get; set; } = null!;

    /// <summary>
    /// Gets/sets an object used to configure the service's health checks, if any
    /// </summary>
    [DataMember(Order = 2, Name = "healthChecks"), JsonPropertyOrder(2), JsonPropertyName("healthChecks"), YamlMember(Order = 2, Alias = "healthChecks")]
    public virtual ServiceHealthCheckConfiguration? HealthChecks { get; set; }

}
