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
/// Represents an object used to configure how a gateway ingests cloud events: the size of requests, the deduplication of retried events, and the rate at which
/// publishers may publish
/// </summary>
[DataContract]
public record CloudEventIngestionPolicy
{

    /// <summary>
    /// Gets/sets the maximum size, in bytes, of an ingestion request. Larger requests are refused with <c>413 Payload Too Large</c>. Defaults to the server's limit
    /// </summary>
    [DataMember(Order = 1, Name = "maxRequestBodySize"), JsonPropertyOrder(1), JsonPropertyName("maxRequestBodySize"), YamlMember(Order = 1, Alias = "maxRequestBodySize")]
    public virtual long? MaxRequestBodySize { get; set; }

    /// <summary>
    /// Gets/sets the maximum amount of cloud events a batch may contain. Larger batches are refused with <c>413 Payload Too Large</c>. Unlimited if not set
    /// </summary>
    [DataMember(Order = 2, Name = "maxBatchSize"), JsonPropertyOrder(2), JsonPropertyName("maxBatchSize"), YamlMember(Order = 2, Alias = "maxBatchSize")]
    public virtual int? MaxBatchSize { get; set; }

    /// <summary>
    /// Gets/sets an object used to configure the deduplication of cloud events, if any. Cloud events are not deduplicated if not set
    /// </summary>
    [DataMember(Order = 3, Name = "deduplication"), JsonPropertyOrder(3), JsonPropertyName("deduplication"), YamlMember(Order = 3, Alias = "deduplication")]
    public virtual CloudEventDeduplicationPolicy? Deduplication { get; set; }

    /// <summary>
    /// Gets/sets the limits of the rate at which cloud events may be published, if any. A cloud event is only ingested if it is within every limit
    /// </summary>
    [DataMember(Order = 4, Name = "rateLimits"), JsonPropertyOrder(4), JsonPropertyName("rateLimits"), YamlMember(Order = 4, Alias = "rateLimits")]
    public virtual List<CloudEventRateLimit>? RateLimits { get; set; }

}

/// <summary>
/// Represents an object used to configure the deduplication of the cloud events ingested by a gateway
/// </summary>
/// <remarks>
/// Cloud events are uniquely identified by their <c>source</c> and <c>id</c>: a cloud event published again within the window, typically by a publisher retrying
/// after a timeout, is answered as if it had been stored, but is not stored again
/// </remarks>
[DataContract]
public record CloudEventDeduplicationPolicy
{

    /// <summary>
    /// Gets/sets how long, after it has been stored, a cloud event is remembered, and its duplicates ignored
    /// </summary>
    [Required, JsonRequired]
    [DataMember(Order = 1, Name = "window", IsRequired = true), JsonPropertyOrder(1), JsonPropertyName("window"), YamlMember(Order = 1, Alias = "window")]
    public virtual Duration Window { get; set; } = null!;

}

/// <summary>
/// Represents a limit of the rate at which cloud events may be published to a gateway
/// </summary>
/// <remarks>
/// Cloud events are counted in fixed windows of the configured period, shared by all replicas of the gateway. A long period, such as a day, makes the limit a quota
/// </remarks>
[DataContract]
public record CloudEventRateLimit
{

    /// <summary>
    /// Gets/sets what the limit applies to. See <see cref="CloudEventRateLimitPartitioning"/>
    /// </summary>
    [Required, JsonRequired]
    [DataMember(Order = 1, Name = "partition", IsRequired = true), JsonPropertyOrder(1), JsonPropertyName("partition"), YamlMember(Order = 1, Alias = "partition")]
    public virtual string Partition { get; set; } = CloudEventRateLimitPartitioning.Publisher;

    /// <summary>
    /// Gets/sets the maximum amount of cloud events that may be published per period, in each partition
    /// </summary>
    [Required, JsonRequired]
    [DataMember(Order = 2, Name = "limit", IsRequired = true), JsonPropertyOrder(2), JsonPropertyName("limit"), YamlMember(Order = 2, Alias = "limit")]
    public virtual long Limit { get; set; }

    /// <summary>
    /// Gets/sets the period the limit applies to
    /// </summary>
    [Required, JsonRequired]
    [DataMember(Order = 3, Name = "period", IsRequired = true), JsonPropertyOrder(3), JsonPropertyName("period"), YamlMember(Order = 3, Alias = "period")]
    public virtual Duration Period { get; set; } = null!;

}

/// <summary>
/// Enumerates what a <see cref="CloudEventRateLimit"/> may apply to
/// </summary>
public static class CloudEventRateLimitPartitioning
{

    /// <summary>
    /// Indicates that the limit applies to each publisher, identified by the name it authenticated with. Anonymous publishers share a single limit
    /// </summary>
    public const string Publisher = "publisher";
    /// <summary>
    /// Indicates that the limit applies to each source of cloud events
    /// </summary>
    public const string Source = "source";

    /// <summary>
    /// Gets an <see cref="IEnumerable{T}"/> containing all supported values
    /// </summary>
    /// <returns>A new <see cref="IEnumerable{T}"/> containing all supported values</returns>
    public static IEnumerable<string> AsEnumerable()
    {
        yield return Publisher;
        yield return Source;
    }

}
