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

using CloudStreams.Core.Resources;

namespace CloudStreams.Core;

/// <summary>
/// Represents a request to run a subscription's filter and mutation against sample or recorded cloud events, without delivering anything (see #224)
/// </summary>
/// <remarks>
/// The cloud events tested are <see cref="Events"/> if set, otherwise the <see cref="Length"/> most recent cloud events of <see cref="Partition"/>, or of the stream
/// </remarks>
[DataContract]
public record SubscriptionTestRequest
{

    /// <summary>
    /// Gets the maximum amount of cloud events a test may run against
    /// </summary>
    public const int MaxLength = 100;

    /// <summary>
    /// Gets the default amount of recorded cloud events a test runs against
    /// </summary>
    public const int DefaultLength = 20;

    /// <summary>
    /// Gets/sets the partition to read the recorded cloud events to test from, if any
    /// </summary>
    [DataMember(Order = 1, Name = "partition"), JsonPropertyName("partition")]
    public virtual PartitionReference? Partition { get; set; }

    /// <summary>
    /// Gets/sets the filter to test, if any. Without one, every cloud event matches
    /// </summary>
    [DataMember(Order = 2, Name = "filter"), JsonPropertyName("filter")]
    public virtual CloudEventFilter? Filter { get; set; }

    /// <summary>
    /// Gets/sets the mutation to test, if any
    /// </summary>
    [DataMember(Order = 3, Name = "mutation"), JsonPropertyName("mutation")]
    public virtual CloudEventMutation? Mutation { get; set; }

    /// <summary>
    /// Gets/sets the sample cloud events to test, if any
    /// </summary>
    [DataMember(Order = 4, Name = "events"), JsonPropertyName("events")]
    public virtual List<CloudEvent>? Events { get; set; }

    /// <summary>
    /// Gets/sets the amount of recorded cloud events to test, when no sample is set. Defaults to <see cref="DefaultLength"/>
    /// </summary>
    [DataMember(Order = 5, Name = "length"), JsonPropertyName("length")]
    public virtual int? Length { get; set; }

}

/// <summary>
/// Represents the result of a <see cref="SubscriptionTestRequest"/>
/// </summary>
[DataContract]
public record SubscriptionTestResult
{

    /// <summary>
    /// Gets/sets the outcome of the test for each cloud event, in the order they have been tested
    /// </summary>
    [DataMember(Order = 1, Name = "items"), JsonPropertyName("items")]
    public virtual List<SubscriptionTestResultItem> Items { get; set; } = [];

    /// <summary>
    /// Gets/sets a message that explains what has not been tested, if anything. A webhook mutation, for instance, is not called
    /// </summary>
    [DataMember(Order = 2, Name = "notice"), JsonPropertyName("notice")]
    public virtual string? Notice { get; set; }

}

/// <summary>
/// Represents the outcome of a subscription test for a single cloud event
/// </summary>
[DataContract]
public record SubscriptionTestResultItem
{

    /// <summary>
    /// Gets/sets the offset of the cloud event in the stream or partition it has been read from, if it is a recorded one
    /// </summary>
    [DataMember(Order = 1, Name = "offset"), JsonPropertyName("offset")]
    public virtual ulong? Offset { get; set; }

    /// <summary>
    /// Gets/sets the cloud event tested
    /// </summary>
    [DataMember(Order = 2, Name = "event"), JsonPropertyName("event")]
    public virtual CloudEvent Event { get; set; } = null!;

    /// <summary>
    /// Gets/sets a boolean indicating whether the filter matches the cloud event, which is to say whether it would be delivered
    /// </summary>
    [DataMember(Order = 3, Name = "matched"), JsonPropertyName("matched")]
    public virtual bool Matched { get; set; }

    /// <summary>
    /// Gets/sets the cloud event the subscriber would receive, if it matched and the mutation has been run
    /// </summary>
    [DataMember(Order = 4, Name = "mutated"), JsonPropertyName("mutated")]
    public virtual CloudEvent? Mutated { get; set; }

    /// <summary>
    /// Gets/sets the error the filter or the mutation raised for the cloud event, if any
    /// </summary>
    [DataMember(Order = 5, Name = "error"), JsonPropertyName("error")]
    public virtual string? Error { get; set; }

}
