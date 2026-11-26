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
/// Represents an endpoint cloud events are delivered to, such as a webhook, a Kafka topic, a NATS subject, an AMQP exchange or another gateway, with the means to
/// authenticate to it, which subscriptions and dead-letter policies refer to by name (see #218)
/// </summary>
/// <remarks>
/// A sink keeps an endpoint and its authentication together, out of the hands of whoever edits the subscriptions that deliver to it. Deleting a sink that is in
/// use is allowed: the subscriptions that deliver to it then fail to, and are retried, dead-lettered or faulted, as with an unreachable endpoint
/// </remarks>
[DataContract]
public record Sink
    : Resource<SinkSpec>
{

    /// <summary>
    /// Gets the <see cref="Sink"/>'s resource type
    /// </summary>
    public static readonly ResourceDefinitionInfo ResourceDefinition = new SinkResourceDefinition()!;

    /// <summary>
    /// Initializes a new <see cref="Sink"/>
    /// </summary>
    public Sink() : base(ResourceDefinition) { }

    /// <summary>
    /// Initializes a new <see cref="Sink"/>
    /// </summary>
    /// <param name="metadata">The <see cref="Sink"/>'s metadata</param>
    /// <param name="spec">The <see cref="Sink"/>'s spec</param>
    public Sink(ResourceMetadata metadata, SinkSpec spec) : base(ResourceDefinition, metadata, spec) { }

}

/// <summary>
/// Represents an object used to configure a <see cref="Sink"/>
/// </summary>
[DataContract]
public record SinkSpec
{

    /// <summary>
    /// Gets/sets the endpoint cloud events are delivered to
    /// </summary>
    [Required, JsonRequired]
    [DataMember(Order = 1, Name = "endpoint", IsRequired = true), JsonPropertyOrder(1), JsonPropertyName("endpoint"), YamlMember(Order = 1, Alias = "endpoint")]
    public virtual Endpoint Endpoint { get; set; } = null!;

}

/// <summary>
/// Represents an endpoint gateways consume cloud events from, such as a Kafka topic, a NATS subject or an AMQP queue, with the means to authenticate to it, which
/// gateways refer to by name (see #218)
/// </summary>
[DataContract]
public record Feed
    : Resource<FeedSpec>
{

    /// <summary>
    /// Gets the <see cref="Feed"/>'s resource type
    /// </summary>
    public static readonly ResourceDefinitionInfo ResourceDefinition = new FeedResourceDefinition()!;

    /// <summary>
    /// Initializes a new <see cref="Feed"/>
    /// </summary>
    public Feed() : base(ResourceDefinition) { }

    /// <summary>
    /// Initializes a new <see cref="Feed"/>
    /// </summary>
    /// <param name="metadata">The <see cref="Feed"/>'s metadata</param>
    /// <param name="spec">The <see cref="Feed"/>'s spec</param>
    public Feed(ResourceMetadata metadata, FeedSpec spec) : base(ResourceDefinition, metadata, spec) { }

}

/// <summary>
/// Represents an object used to configure a <see cref="Feed"/>
/// </summary>
[DataContract]
public record FeedSpec
{

    /// <summary>
    /// Gets/sets the endpoint cloud events are consumed from
    /// </summary>
    [Required, JsonRequired]
    [DataMember(Order = 1, Name = "endpoint", IsRequired = true), JsonPropertyOrder(1), JsonPropertyName("endpoint"), YamlMember(Order = 1, Alias = "endpoint")]
    public virtual Endpoint Endpoint { get; set; } = null!;

}
