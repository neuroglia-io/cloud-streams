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

namespace CloudStreams.Core;

/// <summary>
/// Describes a cloud event that a subscription failed to deliver, and has set aside (see #207)
/// </summary>
[DataContract]
public record DeadLetter
{

    /// <summary>
    /// Gets/sets the offset of the dead letter in the subscription's dead-letter stream
    /// </summary>
    [DataMember(Order = 1, Name = "offset"), JsonPropertyOrder(1), JsonPropertyName("offset"), YamlMember(Order = 1, Alias = "offset")]
    public virtual ulong Offset { get; set; }

    /// <summary>
    /// Gets/sets the offset of the cloud event in the stream or partition the subscription consumes
    /// </summary>
    [DataMember(Order = 2, Name = "eventOffset"), JsonPropertyOrder(2), JsonPropertyName("eventOffset"), YamlMember(Order = 2, Alias = "eventOffset")]
    public virtual ulong EventOffset { get; set; }

    /// <summary>
    /// Gets/sets the cloud event, as it was to be delivered to the subscriber, which is to say mutated, if the subscription mutates cloud events
    /// </summary>
    [Required, JsonRequired]
    [DataMember(Order = 3, Name = "event", IsRequired = true), JsonPropertyOrder(3), JsonPropertyName("event"), YamlMember(Order = 3, Alias = "event")]
    public virtual CloudEvent Event { get; set; } = null!;

    /// <summary>
    /// Gets/sets the problem that describes why the cloud event could not be delivered
    /// </summary>
    [Required, JsonRequired]
    [DataMember(Order = 4, Name = "reason", IsRequired = true), JsonPropertyOrder(4), JsonPropertyName("reason"), YamlMember(Order = 4, Alias = "reason")]
    public virtual ProblemDetails Reason { get; set; } = null!;

    /// <summary>
    /// Gets/sets the date and time at which the cloud event has been dead-lettered
    /// </summary>
    [DataMember(Order = 5, Name = "deadLetteredAt"), JsonPropertyOrder(5), JsonPropertyName("deadLetteredAt"), YamlMember(Order = 5, Alias = "deadLetteredAt")]
    public virtual DateTimeOffset DeadLetteredAt { get; set; }

}
