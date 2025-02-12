﻿// Copyright © 2024-Present The Cloud Streams Authors
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
/// Represents an object used to describe the status of a cloud event subscription
/// </summary>
[DataContract]
public record SubscriptionStatus
{

    /// <summary>
    /// Gets/sets the status phase of the described subscription
    /// </summary>
    [Required, DefaultValue(SubscriptionStatusPhase.Inactive)]
    [DataMember(Order = 1, Name = "phase"), JsonPropertyName("phase"), JsonPropertyOrder(1), YamlMember(Alias = "phase", Order = 1)]
    public virtual SubscriptionStatusPhase Phase { get; set; }

    /// <summary>
    /// Gets/sets the observed generation of the subscription's spec the status describes. Divergence between resource and observed generation values should be handled during a reconciliation loop
    /// </summary>
    [DataMember(Order = 2, Name = "observedGeneration"), JsonPropertyName("observedGeneration"), JsonPropertyOrder(2), YamlMember(Alias = "observedGeneration", Order = 2)]
    public virtual ulong? ObservedGeneration { get; set; }

    /// <summary>
    /// Gets/sets an object used to describe the status of the subscription's cloud event stream
    /// </summary>
    [DataMember(Order = 3, Name = "stream"), JsonPropertyName("stream"), JsonPropertyOrder(3), YamlMember(Alias = "stream", Order = 3)]
    public virtual CloudEventStreamStatus? Stream { get; set; }

    /// <summary>
    /// Gets/sets the subscriber's status
    /// </summary>
    [DataMember(Order = 4, Name = "subscriber"), JsonPropertyName("subscriber"), JsonPropertyOrder(4), YamlMember(Alias = "subscriber", Order = 4)]
    public virtual SubscriberStatus? Subscriber { get; set; }

}
