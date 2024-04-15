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
/// Represents an object used to configure the health checks of a service
/// </summary>
[DataContract]
public record ServiceHealthCheckConfiguration
{

    /// <summary>
    /// Initializes a new <see cref="ServiceHealthCheckConfiguration"/>
    /// </summary>
    public ServiceHealthCheckConfiguration() { }

    /// <summary>
    /// Initializes a new <see cref="ServiceHealthCheckConfiguration"/>
    /// </summary>
    /// <param name="request">An object used to configure the HTTP-based health check request</param>
    /// <param name="interval">The amount of time to wait between every health check request</param>
    public ServiceHealthCheckConfiguration(HttpRequestConfiguration request, Duration? interval = null)
    {
        this.Request = request;
        this.Interval = interval;
    }

    /// <summary>
    /// Gets/sets an object used to configure the HTTP-based health check request
    /// </summary>
    [Required, JsonRequired]
    [DataMember(Order = 1, Name = "request", IsRequired = true), JsonPropertyOrder(1), JsonPropertyName("request"), YamlMember(Order = 1, Alias = "request")]
    public virtual HttpRequestConfiguration Request { get; set; } = null!;

    /// <summary>
    /// Gets/sets the amount of time to wait between every health check request
    /// </summary>
    [DataMember(Order = 2, Name = "interval"), JsonPropertyOrder(2), JsonPropertyName("interval"), YamlMember(Order = 2, Alias = "interval")]
    public virtual Duration? Interval { get; set; }

}
