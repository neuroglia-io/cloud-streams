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
/// Represents an object used to configure a cloud event subscription
/// </summary>
[DataContract]
public record SubscriptionSpec
{

    /// <summary>
    /// Initializes a new <see cref="SubscriptionSpec"/>
    /// </summary>
    public SubscriptionSpec() { }

    /// <summary>
    /// Gets/sets an object used to reference the partition to subscribe to, if any.
    /// If none has been set, the subscription receives all cloud events, regardless of their source, type or subject
    /// </summary>
    [DataMember(Order = 1, Name = "partition"), JsonPropertyName("partition"), YamlMember(Alias = "partition")]
    public virtual PartitionReference? Partition { get; set; }

    /// <summary>
    /// Gets/sets an object used to configure how to filter consumed cloud events
    /// </summary>
    [DataMember(Order = 2, Name = "filter"), JsonPropertyName("filter"), YamlMember(Alias = "filter")]
    public virtual CloudEventFilter? Filter { get; set; }

    /// <summary>
    /// Gets/sets an object used to configure how to mutate consumed cloud events 
    /// </summary>
    [DataMember(Order = 3, Name = "mutation"), JsonPropertyName("mutation"), YamlMember(Alias = "mutation")]
    public virtual CloudEventMutation? Mutation { get; set; }

    /// <summary>
    /// Gets/sets an object used to configure the subscription's cloud event stream
    /// </summary>
    [DataMember(Order = 4, Name = "stream"), JsonPropertyName("stream"), YamlMember(Alias = "stream")]
    public virtual CloudEventStream? Stream { get; set; }

    /// <summary>
    /// Gets/sets an object used to configure the service to dispatch cloud events consumed by the subscription
    /// </summary>
    [Required, JsonRequired]
    [DataMember(Order = 5, Name = "subscriber", IsRequired = true), JsonPropertyName("subscriber"), YamlMember(Alias = "subscriber")]
    public virtual Subscriber Subscriber { get; set; } = null!;

}
