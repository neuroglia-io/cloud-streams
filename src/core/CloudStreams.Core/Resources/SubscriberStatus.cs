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
/// Represents an object used to describe the status of a cloud event subscriber
/// </summary>
[DataContract]
public record SubscriberStatus
{

    /// <summary>
    /// Gets/sets the subscriber's state
    /// </summary>
    [Required, DefaultValue(SubscriberState.Unknown)]
    [DataMember(Order = 1, Name = "state"), JsonPropertyName("state"), JsonPropertyOrder(1), YamlMember(Alias = "state", Order = 1)]
    public virtual SubscriberState State { get; set; } = SubscriberState.Unknown;

    /// <summary>
    /// Gets/sets a reason, if any, describing why the subscriber is in the current state.
    /// </summary>
    [DataMember(Order = 2, Name = "reason"), JsonPropertyName("reason"), JsonPropertyOrder(2), YamlMember(Alias = "reason", Order = 2)]
    public virtual string? Reason { get; set; }

    /// <summary>
    /// Gets/sets the cloud event whose acknowledgement the subscriber has deferred, if any. Delivery is suspended until it is acknowledged, negatively acknowledged, or expires
    /// </summary>
    [DataMember(Order = 3, Name = "pendingAck"), JsonPropertyName("pendingAck"), JsonPropertyOrder(3), YamlMember(Alias = "pendingAck", Order = 3)]
    public virtual PendingAcknowledgement? PendingAck { get; set; }

    /// <summary>
    /// Gets/sets the date and time at which the subscriber last called the acknowledgement endpoints back, if ever.<para></para>
    /// A subscriber that defers an acknowledgement without ever having called back most likely misuses <c>202 Accepted</c>, which is why it faults the subscription rather than being silently redelivered to
    /// </summary>
    [DataMember(Order = 4, Name = "lastAcknowledgedAt"), JsonPropertyName("lastAcknowledgedAt"), JsonPropertyOrder(4), YamlMember(Alias = "lastAcknowledgedAt", Order = 4)]
    public virtual DateTimeOffset? LastAcknowledgedAt { get; set; }

}