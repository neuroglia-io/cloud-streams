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
/// Represents an object used to configure a search of the cloud event stream, or of a partition (see #223)
/// </summary>
/// <remarks>
/// A search reads the stream forwards, from its start, from <see cref="From"/> or from <see cref="Offset"/>, and stops once it has found <see cref="MaxResults"/>
/// matches, has read <see cref="MaxScanned"/> records, or has reached <see cref="To"/> or the end of the stream. The result tells where to continue
/// </remarks>
[DataContract]
public record CloudEventSearchOptions
{

    /// <summary>
    /// Gets the maximum amount of matches a search may return
    /// </summary>
    public const int MaxResultsLimit = 100;

    /// <summary>
    /// Gets the maximum amount of records a search may read
    /// </summary>
    public const int MaxScannedLimit = 10_000;

    /// <summary>
    /// Gets/sets the type of the partition to search, if any
    /// </summary>
    [DataMember(Order = 1, Name = "partitionType"), JsonPropertyName("partitionType")]
    public virtual CloudEventPartitionType? PartitionType { get; set; }

    /// <summary>
    /// Gets/sets the id of the partition to search, if any
    /// </summary>
    [DataMember(Order = 2, Name = "partitionId"), JsonPropertyName("partitionId")]
    public virtual string? PartitionId { get; set; }

    /// <summary>
    /// Gets/sets the offset starting from which to search, which is how a search is continued
    /// </summary>
    [DataMember(Order = 3, Name = "offset"), JsonPropertyName("offset")]
    public virtual ulong? Offset { get; set; }

    /// <summary>
    /// Gets/sets the time starting from which to search, if no offset is set
    /// </summary>
    [DataMember(Order = 4, Name = "from"), JsonPropertyName("from")]
    public virtual DateTimeOffset? From { get; set; }

    /// <summary>
    /// Gets/sets the time at which to stop searching, if any
    /// </summary>
    [DataMember(Order = 5, Name = "to"), JsonPropertyName("to")]
    public virtual DateTimeOffset? To { get; set; }

    /// <summary>
    /// Gets/sets the id of the cloud events to find, if any
    /// </summary>
    [DataMember(Order = 6, Name = "id"), JsonPropertyName("id")]
    public virtual string? Id { get; set; }

    /// <summary>
    /// Gets/sets the regular expressions the context attributes of the cloud events to find must match, if any
    /// </summary>
    [DataMember(Order = 7, Name = "attributes"), JsonPropertyName("attributes")]
    public virtual Dictionary<string, string>? Attributes { get; set; }

    /// <summary>
    /// Gets/sets the runtime expression, evaluated against each record, the cloud events to find must match, if any, e.g. <c>${ .data.total > 100 }</c>
    /// </summary>
    [DataMember(Order = 8, Name = "expression"), JsonPropertyName("expression")]
    public virtual string? Expression { get; set; }

    /// <summary>
    /// Gets/sets the maximum amount of matches to return. Defaults to 20, and cannot exceed <see cref="MaxResultsLimit"/>
    /// </summary>
    [DataMember(Order = 9, Name = "maxResults"), JsonPropertyName("maxResults")]
    public virtual int? MaxResults { get; set; }

    /// <summary>
    /// Gets/sets the maximum amount of records to read. Defaults to and cannot exceed <see cref="MaxScannedLimit"/>
    /// </summary>
    [DataMember(Order = 10, Name = "maxScanned"), JsonPropertyName("maxScanned")]
    public virtual int? MaxScanned { get; set; }

}

/// <summary>
/// Describes the result of a search of the cloud event stream
/// </summary>
[DataContract]
public record CloudEventSearchResult
{

    /// <summary>
    /// Gets/sets the records that matched, in the order of the stream
    /// </summary>
    [DataMember(Order = 1, Name = "records"), JsonPropertyName("records")]
    public virtual List<CloudEventRecord> Records { get; set; } = [];

    /// <summary>
    /// Gets/sets the amount of records read
    /// </summary>
    [DataMember(Order = 2, Name = "scanned"), JsonPropertyName("scanned"), JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public virtual int Scanned { get; set; }

    /// <summary>
    /// Gets/sets the offset to continue the search from, if it has not reached the end of what it searches
    /// </summary>
    [DataMember(Order = 3, Name = "nextOffset"), JsonPropertyName("nextOffset")]
    public virtual ulong? NextOffset { get; set; }

}

/// <summary>
/// Represents a request to deliver a recorded cloud event to the subscriber of a subscription again (see #222)
/// </summary>
[DataContract]
public record CloudEventRedeliveryRequest
{

    /// <summary>
    /// Gets/sets the partition the offset refers to, if any. If not set, the offset refers to the whole stream
    /// </summary>
    [DataMember(Order = 1, Name = "partition"), JsonPropertyName("partition")]
    public virtual PartitionReference? Partition { get; set; }

    /// <summary>
    /// Gets/sets the offset of the cloud event to redeliver
    /// </summary>
    [DataMember(Order = 2, Name = "offset"), JsonPropertyName("offset"), JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public virtual ulong Offset { get; set; }

}

/// <summary>
/// Describes the redelivery of a recorded cloud event to the subscriber of a subscription
/// </summary>
[DataContract]
public record CloudEventRedelivery
{

    /// <summary>
    /// Gets/sets the id of the redelivered cloud event
    /// </summary>
    [DataMember(Order = 1, Name = "id"), JsonPropertyName("id")]
    public virtual string Id { get; set; } = null!;

    /// <summary>
    /// Gets/sets the status code the subscriber responded with
    /// </summary>
    [DataMember(Order = 2, Name = "status"), JsonPropertyName("status")]
    public virtual int Status { get; set; }

}

/// <summary>
/// Describes what became of a published cloud event
/// </summary>
[DataContract]
public record CloudEventPublication
{

    /// <summary>
    /// Gets/sets what became of the cloud event. See <see cref="CloudStreamsHeaders.Outcomes"/>
    /// </summary>
    [DataMember(Order = 1, Name = "outcome"), JsonPropertyName("outcome")]
    public virtual string Outcome { get; set; } = null!;

}
