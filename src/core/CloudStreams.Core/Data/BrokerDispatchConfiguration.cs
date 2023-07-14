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
/// Represents an object used to configure the way a broker should dispatch cloud events
/// </summary>
[DataContract]
public class BrokerDispatchConfiguration
{

    /// <summary>
    /// Initializes a new <see cref="BrokerDispatchConfiguration"/>
    /// </summary>
    public BrokerDispatchConfiguration() { }

    /// <summary>
    /// Initializes a new <see cref="BrokerDispatchConfiguration"/>
    /// </summary>
    /// <param name="retryPolicy">The retry policy that applies by default to all subscriptions managed by the broker</param>
    public BrokerDispatchConfiguration(HttpClientRetryPolicy retryPolicy)
    {
        this.RetryPolicy = retryPolicy;
    }

    /// <summary>
    /// Gets/sets the retry policy that applies by default to all subscriptions managed by the broker
    /// </summary>
    [DataMember(Order = 1, Name = "retryPolicy"), JsonPropertyOrder(1), JsonPropertyName("retryPolicy"), YamlMember(Order = 1, Alias = "retryPolicy")]
    public virtual HttpClientRetryPolicy? RetryPolicy { get; set; }

}
