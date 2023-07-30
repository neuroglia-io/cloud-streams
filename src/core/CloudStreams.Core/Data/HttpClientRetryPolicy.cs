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
/// Represents an object used to configure the retry policy for an http client
/// </summary>
[DataContract]
public record HttpClientRetryPolicy
    : RetryPolicy
{

    /// <summary>
    /// Initializes a new <see cref="HttpClientRetryPolicy"/>
    /// </summary>
    public HttpClientRetryPolicy() { }

    /// <summary>
    /// Initializes a new <see cref="HttpClientRetryPolicy"/>
    /// </summary>
    /// <param name="statusCodes">A list containing the http status codes the retry policy applies to. If not set, the policy will apply to all non-success (200-300) status codes</param>
    /// <param name="circuitBreaker">An object that configures the client's circuit breaker, if any</param>
    public HttpClientRetryPolicy(IEnumerable<int>? statusCodes = null, CircuitBreakerPolicy? circuitBreaker = null)
    {
        this.StatusCodes = statusCodes?.ToList();
        this.CircuitBreaker = circuitBreaker;
    }

    /// <summary>
    /// Gets/sets a list containing the http status codes the retry policy applies to. If not set, the policy will apply to all non-success (200-300) status codes
    /// </summary>
    [DataMember(Order = 1, Name = "statusCodes"), JsonPropertyName("statusCodes"), YamlMember(Alias = "statusCodes")]
    public virtual List<int>? StatusCodes { get; set; }

    /// <summary>
    /// Gets/sets an object that configures the client's circuit breaker, if any
    /// </summary>
    [DataMember(Order = 2, Name = "circuitBreaker"), JsonPropertyName("circuitBreaker"), YamlMember(Alias = "circuitBreaker")]
    public virtual CircuitBreakerPolicy? CircuitBreaker { get; set; }

}
