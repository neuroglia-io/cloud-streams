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
/// Represents an object used to configure the delivery of cloud events to a subscriber in batches, formatted as <c>application/cloudevents-batch+json</c> (see #212)
/// </summary>
[DataContract]
public record SubscriberBatchConfiguration
{

    /// <summary>
    /// Gets the default maximum amount of time to wait for a batch to fill
    /// </summary>
    public static readonly TimeSpan DefaultLinger = TimeSpan.Zero;

    /// <summary>
    /// Gets/sets the maximum amount of cloud events per batch
    /// </summary>
    [Required, JsonRequired]
    [DataMember(Order = 1, Name = "maxSize", IsRequired = true), JsonPropertyOrder(1), JsonPropertyName("maxSize"), YamlMember(Order = 1, Alias = "maxSize")]
    public virtual int MaxSize { get; set; }

    /// <summary>
    /// Gets/sets the maximum amount of time to wait for a batch to fill, once the subscription has caught up with its stream. Defaults to none: whatever has been
    /// published is delivered at once
    /// </summary>
    [DataMember(Order = 2, Name = "linger"), JsonPropertyOrder(2), JsonPropertyName("linger"), YamlMember(Order = 2, Alias = "linger")]
    public virtual Duration? Linger { get; set; }

}

/// <summary>
/// Represents an object used to configure the delivery of cloud events to a subscriber in parallel: cloud events that share the same key are delivered in the order of
/// the stream, while cloud events of different keys are delivered concurrently (see #212)
/// </summary>
[DataContract]
public record SubscriberOrderingConfiguration
{

    /// <summary>
    /// Gets the default maximum amount of keys whose cloud events are delivered concurrently
    /// </summary>
    public const int DefaultMaxConcurrency = 8;

    /// <summary>
    /// Gets/sets the name of the context attribute whose value is the key cloud events are ordered by, e.g. <c>subject</c>. Cloud events that do not define it share
    /// a single key
    /// </summary>
    [Required, JsonRequired]
    [DataMember(Order = 1, Name = "key", IsRequired = true), JsonPropertyOrder(1), JsonPropertyName("key"), YamlMember(Order = 1, Alias = "key")]
    public virtual string Key { get; set; } = null!;

    /// <summary>
    /// Gets/sets the maximum amount of keys whose cloud events are delivered concurrently. Defaults to 8
    /// </summary>
    [DataMember(Order = 2, Name = "maxConcurrency"), JsonPropertyOrder(2), JsonPropertyName("maxConcurrency"), YamlMember(Order = 2, Alias = "maxConcurrency")]
    public virtual int? MaxConcurrency { get; set; }

}
