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

using System.ComponentModel;
using System.Text.Json;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace CloudStreams.Core.Resources;

/// <summary>
/// Represents an endpoint, after the endpoints of the Serverless Workflow specification: either an address, or an object with an address and how to authenticate
/// to it (see #218)
/// </summary>
/// <remarks>
/// <code>
/// endpoint: https://orders.example.com/events
/// endpoint:
///   uri: kafka://kafka:9092/orders
///   authentication:
///     basic:
///       use: orders-kafka
/// </code>
/// The scheme of the address selects the transport. See <see cref="TransportAddress"/>
/// </remarks>
[JsonConverter(typeof(EndpointJsonConverter))]
public record Endpoint
    : IYamlConvertible
{

    /// <summary>
    /// Initializes a new <see cref="Endpoint"/>
    /// </summary>
    public Endpoint() { }

    /// <summary>
    /// Initializes a new <see cref="Endpoint"/>
    /// </summary>
    /// <param name="uri">The address of the endpoint</param>
    /// <param name="authentication">How to authenticate to the endpoint, if needed</param>
    public Endpoint(Uri uri, AuthenticationPolicy? authentication = null)
    {
        this.Uri = uri;
        this.Authentication = authentication;
    }

    /// <summary>
    /// Gets/sets the address of the endpoint
    /// </summary>
    public virtual Uri Uri { get; set; } = null!;

    /// <summary>
    /// Gets/sets how to authenticate to the endpoint, if needed
    /// </summary>
    public virtual AuthenticationPolicy? Authentication { get; set; }

    /// <summary>
    /// Converts the specified address into a new <see cref="Endpoint"/>, without authentication
    /// </summary>
    /// <param name="uri">The address to convert</param>
    public static implicit operator Endpoint(Uri uri) => new(uri);

    /// <inheritdoc/>
    public override string ToString() => this.Uri?.OriginalString ?? string.Empty;

    void IYamlConvertible.Read(IParser parser, Type expectedType, ObjectDeserializer nestedObjectDeserializer)
    {
        if (parser.TryConsume<Scalar>(out var scalar))
        {
            this.Uri = new(scalar.Value, UriKind.RelativeOrAbsolute);
            this.Authentication = null;
            return;
        }
        var definition = (EndpointDefinition?)nestedObjectDeserializer(typeof(EndpointDefinition)) ?? throw new YamlException("An endpoint must be an address, or an object with an address");
        this.Uri = definition.Uri;
        this.Authentication = definition.Authentication;
    }

    void IYamlConvertible.Write(IEmitter emitter, ObjectSerializer nestedObjectSerializer)
    {
        if (this.Authentication == null) emitter.Emit(new Scalar(this.ToString()));
        else nestedObjectSerializer(new EndpointDefinition() { Uri = this.Uri, Authentication = this.Authentication }, typeof(EndpointDefinition));
    }

}

/// <summary>
/// Represents the object form of an <see cref="Endpoint"/>, used to serialize it
/// </summary>
[DataContract, EditorBrowsable(EditorBrowsableState.Never)]
public record EndpointDefinition
{

    /// <summary>
    /// Gets/sets the address of the endpoint
    /// </summary>
    [Required, JsonRequired]
    [DataMember(Order = 1, Name = "uri", IsRequired = true), JsonPropertyOrder(1), JsonPropertyName("uri"), YamlMember(Order = 1, Alias = "uri")]
    public virtual Uri Uri { get; set; } = null!;

    /// <summary>
    /// Gets/sets how to authenticate to the endpoint, if needed
    /// </summary>
    [DataMember(Order = 2, Name = "authentication"), JsonPropertyOrder(2), JsonPropertyName("authentication"), YamlMember(Order = 2, Alias = "authentication")]
    public virtual AuthenticationPolicy? Authentication { get; set; }

}

/// <summary>
/// Represents the <see cref="JsonConverter"/> of <see cref="Endpoint"/>s, which are either an address or an object
/// </summary>
public class EndpointJsonConverter
    : JsonConverter<Endpoint>
{

    /// <inheritdoc/>
    public override Endpoint? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return null;
            case JsonTokenType.String:
                return new(new Uri(reader.GetString()!, UriKind.RelativeOrAbsolute));
            case JsonTokenType.StartObject:
                var definition = JsonSerializer.Deserialize<EndpointDefinition>(ref reader, options) ?? throw new JsonException("An endpoint must be an address, or an object with an address");
                return new(definition.Uri, definition.Authentication);
            default:
                throw new JsonException($"Unexpected token '{reader.TokenType}' while reading an endpoint: expected an address, or an object with an address");
        }
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, Endpoint value, JsonSerializerOptions options)
    {
        if (value.Authentication == null) writer.WriteStringValue(value.ToString());
        else JsonSerializer.Serialize(writer, new EndpointDefinition() { Uri = value.Uri, Authentication = value.Authentication }, options);
    }

}

/// <summary>
/// Represents a feed a gateway consumes: either the name of a <see cref="Feed"/> resource, or a feed of its own, with a name and an endpoint (see #218)
/// </summary>
/// <remarks>
/// <code>
/// feeds:
///   - legacy-orders                  # a Feed resource
///   - name: payments                 # a feed of the gateway's own
///     endpoint: nats://nats:4222/payments.>?jetstream=true
/// </code>
/// </remarks>
[JsonConverter(typeof(FeedReferenceJsonConverter))]
public record FeedReference
    : IYamlConvertible
{

    /// <summary>
    /// Gets/sets the name of the <see cref="Resources.Feed"/> resource the reference refers to, if any
    /// </summary>
    public virtual string? Feed { get; set; }

    /// <summary>
    /// Gets/sets the name of the gateway's own feed, if the reference does not refer to a <see cref="Resources.Feed"/> resource
    /// </summary>
    public virtual string? Name { get; set; }

    /// <summary>
    /// Gets/sets the endpoint of the gateway's own feed, if the reference does not refer to a <see cref="Resources.Feed"/> resource
    /// </summary>
    public virtual Endpoint? Endpoint { get; set; }

    /// <summary>
    /// Gets the name of the feed, which identifies it as the publisher of the cloud events it carries: the name of the resource, or that of the gateway's own feed
    /// </summary>
    [JsonIgnore, YamlIgnore]
    public virtual string? EffectiveName => this.Feed ?? this.Name;

    /// <summary>
    /// Converts the specified name into a reference to the <see cref="Resources.Feed"/> resource of that name
    /// </summary>
    /// <param name="feed">The name of the resource</param>
    public static implicit operator FeedReference(string feed) => new() { Feed = feed };

    void IYamlConvertible.Read(IParser parser, Type expectedType, ObjectDeserializer nestedObjectDeserializer)
    {
        if (parser.TryConsume<Scalar>(out var scalar))
        {
            this.Feed = scalar.Value;
            return;
        }
        var definition = (FeedDefinition?)nestedObjectDeserializer(typeof(FeedDefinition)) ?? throw new YamlException("A feed must be the name of a feed, or an object with a name and an endpoint");
        this.Name = definition.Name;
        this.Endpoint = definition.Endpoint;
    }

    void IYamlConvertible.Write(IEmitter emitter, ObjectSerializer nestedObjectSerializer)
    {
        if (this.Feed != null) emitter.Emit(new Scalar(this.Feed));
        else nestedObjectSerializer(new FeedDefinition() { Name = this.Name!, Endpoint = this.Endpoint! }, typeof(FeedDefinition));
    }

}

/// <summary>
/// Represents the object form of a <see cref="FeedReference"/>, a feed of a gateway's own, used to serialize it
/// </summary>
[DataContract, EditorBrowsable(EditorBrowsableState.Never)]
public record FeedDefinition
{

    /// <summary>
    /// Gets/sets the name of the feed, which identifies it as the publisher of the cloud events it carries
    /// </summary>
    [Required, JsonRequired]
    [DataMember(Order = 1, Name = "name", IsRequired = true), JsonPropertyOrder(1), JsonPropertyName("name"), YamlMember(Order = 1, Alias = "name")]
    public virtual string Name { get; set; } = null!;

    /// <summary>
    /// Gets/sets the endpoint of the feed
    /// </summary>
    [Required, JsonRequired]
    [DataMember(Order = 2, Name = "endpoint", IsRequired = true), JsonPropertyOrder(2), JsonPropertyName("endpoint"), YamlMember(Order = 2, Alias = "endpoint")]
    public virtual Endpoint Endpoint { get; set; } = null!;

}

/// <summary>
/// Represents the <see cref="JsonConverter"/> of <see cref="FeedReference"/>s, which are either the name of a feed or an object
/// </summary>
public class FeedReferenceJsonConverter
    : JsonConverter<FeedReference>
{

    /// <inheritdoc/>
    public override FeedReference? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return null;
            case JsonTokenType.String:
                return new() { Feed = reader.GetString() };
            case JsonTokenType.StartObject:
                var definition = JsonSerializer.Deserialize<FeedDefinition>(ref reader, options) ?? throw new JsonException("A feed must be the name of a feed, or an object with a name and an endpoint");
                return new() { Name = definition.Name, Endpoint = definition.Endpoint };
            default:
                throw new JsonException($"Unexpected token '{reader.TokenType}' while reading a feed: expected the name of a feed, or an object with a name and an endpoint");
        }
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, FeedReference value, JsonSerializerOptions options)
    {
        if (value.Feed != null) writer.WriteStringValue(value.Feed);
        else JsonSerializer.Serialize(writer, new FeedDefinition() { Name = value.Name!, Endpoint = value.Endpoint! }, options);
    }

}
