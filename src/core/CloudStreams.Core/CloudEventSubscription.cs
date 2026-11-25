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

using System.Text.Json.Nodes;

namespace CloudStreams.Core;

/// <summary>
/// Represents a subscription, as defined by the CloudEvents Subscriptions API (see #219)
/// </summary>
/// <remarks>See <see href="https://github.com/cloudevents/spec/blob/main/subscriptions/spec.md"/></remarks>
[DataContract]
public record CloudEventSubscription
{

    /// <summary>
    /// Gets the only protocol Cloud Streams delivers cloud events with
    /// </summary>
    public const string HttpProtocol = "HTTP";

    /// <summary>
    /// Gets/sets the subscription's id, which the subscription manager assigns. Ignored when creating a subscription
    /// </summary>
    [DataMember(Order = 1, Name = "id"), JsonPropertyOrder(1), JsonPropertyName("id")]
    public virtual string? Id { get; set; }

    /// <summary>
    /// Gets/sets the source the cloud events delivered must have, if any
    /// </summary>
    [DataMember(Order = 2, Name = "source"), JsonPropertyOrder(2), JsonPropertyName("source")]
    public virtual string? Source { get; set; }

    /// <summary>
    /// Gets/sets the types the cloud events delivered must have one of, if any
    /// </summary>
    [DataMember(Order = 3, Name = "types"), JsonPropertyOrder(3), JsonPropertyName("types")]
    public virtual List<string>? Types { get; set; }

    /// <summary>
    /// Gets/sets the implementation-specific configuration of the subscription, if any
    /// </summary>
    [DataMember(Order = 4, Name = "config"), JsonPropertyOrder(4), JsonPropertyName("config")]
    public virtual JsonObject? Config { get; set; }

    /// <summary>
    /// Gets/sets the filter expressions the cloud events delivered must all match, if any. Each is an object whose single property is named after its dialect
    /// </summary>
    [DataMember(Order = 5, Name = "filters"), JsonPropertyOrder(5), JsonPropertyName("filters")]
    public virtual List<JsonObject>? Filters { get; set; }

    /// <summary>
    /// Gets/sets the uri of the service to deliver cloud events to
    /// </summary>
    [DataMember(Order = 6, Name = "sink"), JsonPropertyOrder(6), JsonPropertyName("sink")]
    public virtual string? Sink { get; set; }

    /// <summary>
    /// Gets/sets the credentials to deliver cloud events with, if any. Not supported by Cloud Streams
    /// </summary>
    [DataMember(Order = 7, Name = "sinkcredential"), JsonPropertyOrder(7), JsonPropertyName("sinkcredential")]
    public virtual JsonObject? SinkCredential { get; set; }

    /// <summary>
    /// Gets/sets the protocol to deliver cloud events with. Cloud Streams only supports <see cref="HttpProtocol"/>
    /// </summary>
    [DataMember(Order = 8, Name = "protocol"), JsonPropertyOrder(8), JsonPropertyName("protocol")]
    public virtual string? Protocol { get; set; }

    /// <summary>
    /// Gets/sets the settings of the protocol, if any
    /// </summary>
    [DataMember(Order = 9, Name = "protocolsettings"), JsonPropertyOrder(9), JsonPropertyName("protocolsettings")]
    public virtual JsonObject? ProtocolSettings { get; set; }

}
