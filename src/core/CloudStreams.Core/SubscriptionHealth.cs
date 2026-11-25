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
/// Represents a flat, monitoring-friendly description of the health of a <see cref="Resources.Subscription"/>
/// </summary>
/// <remarks>
/// Every property is always serialized, <c>null</c> included, so that monitoring tools such as Grafana see a consistent set of fields
/// </remarks>
[DataContract]
public record SubscriptionHealth
{

    /// <summary>
    /// Gets the value of <see cref="Phase"/> when the subscription's stream has faulted
    /// </summary>
    public const string FailedPhase = "failed";

    /// <summary>
    /// Gets/sets the name of the subscription
    /// </summary>
    [DataMember(Order = 1, Name = "name"), JsonPropertyOrder(1), JsonPropertyName("name"), JsonIgnore(Condition = JsonIgnoreCondition.Never), YamlMember(Order = 1, Alias = "name")]
    public virtual string Name { get; set; } = null!;

    /// <summary>
    /// Gets/sets the namespace of the subscription, if any. Subscriptions are cluster-scoped, so this is typically null
    /// </summary>
    [DataMember(Order = 2, Name = "namespace"), JsonPropertyOrder(2), JsonPropertyName("namespace"), JsonIgnore(Condition = JsonIgnoreCondition.Never), YamlMember(Order = 2, Alias = "namespace")]
    public virtual string? Namespace { get; set; }

    /// <summary>
    /// Gets/sets the phase of the subscription: <c>active</c>, <c>inactive</c> or <c>disabled</c>, or <c>failed</c> if its stream has faulted
    /// </summary>
    [DataMember(Order = 3, Name = "phase"), JsonPropertyOrder(3), JsonPropertyName("phase"), JsonIgnore(Condition = JsonIgnoreCondition.Never), YamlMember(Order = 3, Alias = "phase")]
    public virtual string Phase { get; set; } = null!;

    /// <summary>
    /// Gets/sets the id of the partition the subscription consumes, if any
    /// </summary>
    [DataMember(Order = 4, Name = "partitionId"), JsonPropertyOrder(4), JsonPropertyName("partitionId"), JsonIgnore(Condition = JsonIgnoreCondition.Never), YamlMember(Order = 4, Alias = "partitionId")]
    public virtual string? PartitionId { get; set; }

    /// <summary>
    /// Gets/sets the acknowledged offset, which is to say the offset of the next event to deliver, if the subscription tracks one
    /// </summary>
    [DataMember(Order = 5, Name = "ackedOffset"), JsonPropertyOrder(5), JsonPropertyName("ackedOffset"), JsonIgnore(Condition = JsonIgnoreCondition.Never), YamlMember(Order = 5, Alias = "ackedOffset")]
    public virtual ulong? AckedOffset { get; set; }

    /// <summary>
    /// Gets/sets the length of the stream or partition the subscription consumes, if available
    /// </summary>
    [DataMember(Order = 6, Name = "partitionLength"), JsonPropertyOrder(6), JsonPropertyName("partitionLength"), JsonIgnore(Condition = JsonIgnoreCondition.Never), YamlMember(Order = 6, Alias = "partitionLength")]
    public virtual ulong? PartitionLength { get; set; }

    /// <summary>
    /// Gets/sets the amount of events the subscription has yet to acknowledge, which is to say <see cref="PartitionLength"/> minus <see cref="AckedOffset"/>, if both are known
    /// </summary>
    [DataMember(Order = 7, Name = "lag"), JsonPropertyOrder(7), JsonPropertyName("lag"), JsonIgnore(Condition = JsonIgnoreCondition.Never), YamlMember(Order = 7, Alias = "lag")]
    public virtual ulong? Lag { get; set; }

    /// <summary>
    /// Gets/sets the state of the subscriber, <c>reachable</c> or <c>unreachable</c>, if known
    /// </summary>
    [DataMember(Order = 8, Name = "subscriberState"), JsonPropertyOrder(8), JsonPropertyName("subscriberState"), JsonIgnore(Condition = JsonIgnoreCondition.Never), YamlMember(Order = 8, Alias = "subscriberState")]
    public virtual string? SubscriberState { get; set; }

    /// <summary>
    /// Gets/sets the reason why the subscriber is unreachable or, if the subscription has failed, why its stream has faulted
    /// </summary>
    [DataMember(Order = 9, Name = "subscriberReason"), JsonPropertyOrder(9), JsonPropertyName("subscriberReason"), JsonIgnore(Condition = JsonIgnoreCondition.Never), YamlMember(Order = 9, Alias = "subscriberReason")]
    public virtual string? SubscriberReason { get; set; }

    /// <summary>
    /// Gets/sets the amount of events the subscription will never deliver because they expired before it got to them, if any: its offset is before the first event its
    /// stream or partition retains (see #206)
    /// </summary>
    [DataMember(Order = 10, Name = "expiredEvents"), JsonPropertyOrder(10), JsonPropertyName("expiredEvents"), YamlMember(Order = 10, Alias = "expiredEvents")]
    public virtual ulong? ExpiredEvents { get; set; }

}
