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
/// Represents an object used to configure the consumer of cloud events produced by a subscription
/// </summary>
[DataContract]
public record Subscriber
{

    /// <summary>
    /// Gets/sets the address of the dispatch consumed cloud events to
    /// </summary>
    [Required, JsonRequired]
    [DataMember(Order = 1, Name = "uri", IsRequired = true), JsonPropertyName("uri"), YamlMember(Alias = "uri")]
    public virtual Uri Uri { get; set; } = null!;

    /// <summary>
    /// Gets/sets the maximum amount of events, if any, that can be dispatched per second to the subscriber
    /// </summary>
    [DataMember(Order = 2, Name = "rateLimit"), JsonPropertyName("rateLimit"), YamlMember(Alias = "rateLimit")]
    public virtual double? RateLimit { get; set; }

    /// <summary>
    /// Gets/sets the retry policy to use when dispatching cloud events to the subscriber. If not set, will fallback to the broker's default retry policy
    /// </summary>
    [DataMember(Order = 3, Name = "retryPolicy"), JsonPropertyName("retryPolicy"), YamlMember(Alias = "retryPolicy")]
    public virtual HttpClientRetryPolicy? RetryPolicy { get; set; }

    /// <summary>
    /// Gets/sets the maximum amount of time to wait for the subscriber to respond to a single delivery attempt. If not set, will fallback to the broker's default dispatch timeout.<para></para>
    /// A delivery attempt that times out is considered failed and is retried according to the retry policy
    /// </summary>
    [DataMember(Order = 4, Name = "timeout"), JsonPropertyName("timeout"), YamlMember(Alias = "timeout")]
    public virtual Duration? Timeout { get; set; }

    /// <summary>
    /// Gets/sets an object used to configure the deferred acknowledgement of the cloud events delivered to the subscriber. If not set, will fallback to the broker's default configuration
    /// </summary>
    [DataMember(Order = 5, Name = "ack"), JsonPropertyName("ack"), YamlMember(Alias = "ack")]
    public virtual SubscriberAcknowledgementConfiguration? Ack { get; set; }

}