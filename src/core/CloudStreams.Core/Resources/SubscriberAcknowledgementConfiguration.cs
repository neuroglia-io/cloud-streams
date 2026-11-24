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
/// Represents an object used to configure the deferred acknowledgement of the cloud events delivered to a subscriber
/// </summary>
/// <remarks>
/// A subscriber defers the acknowledgement of a cloud event by responding <c>202 Accepted</c>, then calls the acknowledgement endpoint back once it has
/// processed it. Delivery is suspended in the meantime
/// </remarks>
[DataContract]
public record SubscriberAcknowledgementConfiguration
{

    /// <summary>
    /// Gets the default amount of time a cloud event may remain unacknowledged before the broker takes it back
    /// </summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets/sets the maximum amount of time a cloud event whose acknowledgement has been deferred may remain unacknowledged before it is redelivered
    /// </summary>
    [DataMember(Order = 1, Name = "timeout"), JsonPropertyOrder(1), JsonPropertyName("timeout"), YamlMember(Order = 1, Alias = "timeout")]
    public virtual Duration? Timeout { get; set; }

}

/// <summary>
/// Represents an object used to configure the deferred acknowledgement of the cloud events dispatched by a broker
/// </summary>
[DataContract]
public record BrokerAcknowledgementConfiguration
    : SubscriberAcknowledgementConfiguration
{

    /// <summary>
    /// Gets/sets the base uri, as reachable by subscribers, of the gateway that exposes the acknowledgement endpoints, e.g. <c>https://cloud-streams.example.com</c>.<para></para>
    /// When set, it is advertised to subscribers with every delivery, in the <c>cloudstreams-ack-uri</c> header
    /// </summary>
    [DataMember(Order = 2, Name = "callbackUri"), JsonPropertyOrder(2), JsonPropertyName("callbackUri"), YamlMember(Order = 2, Alias = "callbackUri")]
    public virtual Uri? CallbackUri { get; set; }

}
