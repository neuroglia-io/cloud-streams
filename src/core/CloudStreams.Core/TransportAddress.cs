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

using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace CloudStreams.Core;

/// <summary>
/// Enumerates the transports cloud events can be delivered over, or consumed from, which the scheme of an address selects (see #218)
/// </summary>
public static class TransportSchemes
{

    /// <summary>
    /// Gets the scheme of HTTP addresses
    /// </summary>
    public const string Http = "http";
    /// <summary>
    /// Gets the scheme of HTTPS addresses
    /// </summary>
    public const string Https = "https";
    /// <summary>
    /// Gets the scheme of the addresses of Kafka topics: <c>kafka://{bootstrap server}/{topic}</c>
    /// </summary>
    public const string Kafka = "kafka";
    /// <summary>
    /// Gets the scheme of the addresses of NATS subjects: <c>nats://{server}/{subject}</c>
    /// </summary>
    public const string Nats = "nats";
    /// <summary>
    /// Gets the scheme of the addresses of AMQP 0-9-1 (RabbitMQ) exchanges and queues: <c>amqp://{host}/{virtual host}?exchange={exchange}</c>
    /// </summary>
    public const string Amqp = "amqp";
    /// <summary>
    /// Gets the scheme of the addresses of AMQP 0-9-1 (RabbitMQ) exchanges and queues, over TLS
    /// </summary>
    public const string Amqps = "amqps";
    /// <summary>
    /// Gets the scheme of the addresses of other Cloud Streams gateways: <c>cloudstreams://{host}</c>
    /// </summary>
    public const string CloudStreams = "cloudstreams";

    /// <summary>
    /// Gets the schemes of the transports cloud events can be delivered over
    /// </summary>
    public static IReadOnlyList<string> Sinks { get; } = [Http, Https, Kafka, Nats, Amqp, Amqps, CloudStreams];

    /// <summary>
    /// Gets the schemes of the transports gateways can consume cloud events from
    /// </summary>
    public static IReadOnlyList<string> Sources { get; } = [Kafka, Nats, Amqp, Amqps];

    /// <summary>
    /// Gets the authentication schemes the transport of the specified scheme supports. See <see cref="Resources.AuthenticationScheme"/>
    /// </summary>
    /// <param name="scheme">The scheme of the address</param>
    /// <returns>The authentication schemes the transport supports</returns>
    public static IReadOnlyList<string> GetAuthenticationSchemes(string? scheme) => scheme switch
    {
        Http or Https => [Resources.AuthenticationScheme.Basic, Resources.AuthenticationScheme.Bearer, Resources.AuthenticationScheme.ApiKey, Resources.AuthenticationScheme.OAuth2, Resources.AuthenticationScheme.Oidc],
        CloudStreams => [Resources.AuthenticationScheme.ApiKey, Resources.AuthenticationScheme.Bearer, Resources.AuthenticationScheme.OAuth2, Resources.AuthenticationScheme.Oidc],
        Kafka => [Resources.AuthenticationScheme.Basic, Resources.AuthenticationScheme.Bearer, Resources.AuthenticationScheme.OAuth2, Resources.AuthenticationScheme.Oidc],
        Nats => [Resources.AuthenticationScheme.Basic, Resources.AuthenticationScheme.Bearer],
        Amqp or Amqps => [Resources.AuthenticationScheme.Basic, Resources.AuthenticationScheme.Bearer, Resources.AuthenticationScheme.OAuth2, Resources.AuthenticationScheme.Oidc],
        _ => []
    };

}

/// <summary>
/// Represents the parsed address of a non-HTTP transport, which is a URI whose scheme selects the transport (see #218)
/// </summary>
/// <remarks>
/// <para>Addresses never carry credentials, since resources can be read by anyone allowed to read them: endpoints configure how to authenticate separately, and sinks and feeds hold endpoints together with the credentials
/// used to connect to it, which the broker or the gateway reads when it connects.</para>
/// <para>Supported parameters:</para>
/// <list type="bullet">
/// <item><description><c>kafka://{server}[:{port}]/{topic}</c>, port 9092 by default. <c>servers</c> lists additional bootstrap servers, comma-separated. <c>tls=true</c> enables TLS,
/// and credentials enable SASL, with the <c>mechanism</c> <c>plain</c> (default), <c>scram-sha-256</c> or <c>scram-sha-512</c>. <c>group</c> sets the consumer group of a gateway,
/// its name by default.</description></item>
/// <item><description><c>nats://{server}[:{port}]/{subject}</c>, port 4222 by default. <c>jetstream=true</c> waits for JetStream to acknowledge published events, and
/// consumes with a durable consumer of the stream the subject belongs to. <c>tls=true</c> enables TLS. <c>queue</c> sets the queue group of a gateway, its name by
/// default.</description></item>
/// <item><description><c>amqp[s]://{host}[:{port}]/[{virtual host}]</c>, port 5672 (5671 over TLS) by default. To deliver, <c>exchange</c> names the exchange, the default one
/// if empty, and <c>routingKey</c> the routing key, the event's type by default. To consume, <c>queue</c> names the queue.</description></item>
/// <item><description><c>cloudstreams://{host}[:{port}][/{path}]</c>, delivers to the gateway served at <c>https://{host}:{port}/{path}</c>, or over HTTP with
/// <c>tls=false</c>. Credentials provide its API key.</description></item>
/// </list>
/// </remarks>
public sealed record TransportAddress
{

    static readonly Regex KafkaTopicPattern = new("^[a-zA-Z0-9._-]{1,249}$", RegexOptions.Compiled);
    static readonly Regex NatsSubjectPattern = new(@"^[^\s.*>]+(\.[^\s.*>]+)*$", RegexOptions.Compiled);
    static readonly Regex NatsSubscriptionPattern = new(@"^([^\s.*>]+|\*)(\.([^\s.*>]+|\*))*(\.>)?$|^>$", RegexOptions.Compiled);

    TransportAddress(Uri uri, string scheme, IReadOnlyDictionary<string, string> parameters)
    {
        this.Uri = uri;
        this.Scheme = scheme;
        this.Parameters = parameters;
    }

    /// <summary>
    /// Gets the address
    /// </summary>
    public Uri Uri { get; }

    /// <summary>
    /// Gets the scheme of the address, which selects the transport. See <see cref="TransportSchemes"/>
    /// </summary>
    public string Scheme { get; }

    /// <summary>
    /// Gets the query parameters of the address, whose names are case-insensitive
    /// </summary>
    public IReadOnlyDictionary<string, string> Parameters { get; }

    /// <summary>
    /// Gets the host and port of the address, with the default port of its transport if it has none
    /// </summary>
    public string Server => $"{this.Uri.Host}:{(this.Uri.IsDefaultPort || this.Uri.Port < 0 ? this.DefaultPort() : this.Uri.Port)}";

    /// <summary>
    /// Gets the path of the address, without its leading slash: a Kafka topic, a NATS subject, an AMQP virtual host or the path of a gateway
    /// </summary>
    public string Path => Uri.UnescapeDataString(this.Uri.AbsolutePath.TrimStart('/'));

    /// <summary>
    /// Gets a boolean indicating whether the transport is secured with TLS
    /// </summary>
    public bool Tls => this.Scheme switch
    {
        TransportSchemes.Amqps => true,
        TransportSchemes.Amqp => false,
        TransportSchemes.CloudStreams => !string.Equals(this.GetParameter("tls"), "false", StringComparison.OrdinalIgnoreCase),
        _ => string.Equals(this.GetParameter("tls"), "true", StringComparison.OrdinalIgnoreCase)
    };

    /// <summary>
    /// Gets the value of the specified query parameter, if any
    /// </summary>
    /// <param name="name">The name of the parameter</param>
    /// <returns>The value of the parameter, or null if the address does not have it, or if it is empty</returns>
    public string? GetParameter(string name) => this.Parameters.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;

    /// <summary>
    /// Gets a boolean indicating whether the specified URI is the address of a non-HTTP transport
    /// </summary>
    /// <param name="uri">The URI to check</param>
    /// <returns>A boolean indicating whether the URI is the address of a non-HTTP transport</returns>
    public static bool IsTransport(Uri? uri) => uri != null && uri.IsAbsoluteUri && uri.Scheme is TransportSchemes.Kafka or TransportSchemes.Nats or TransportSchemes.Amqp or TransportSchemes.Amqps or TransportSchemes.CloudStreams;

    /// <summary>
    /// Parses the specified URI as the address of a non-HTTP transport
    /// </summary>
    /// <param name="uri">The URI to parse</param>
    /// <param name="consume">A boolean indicating whether events are consumed from the address, rather than delivered to it</param>
    /// <returns>The parsed address</returns>
    /// <exception cref="FormatException">The URI is not a valid address</exception>
    public static TransportAddress Parse(Uri uri, bool consume = false) => TryParse(uri, consume, out var address, out var error) ? address : throw new FormatException(error);

    /// <summary>
    /// Attempts to parse the specified URI as the address of a non-HTTP transport
    /// </summary>
    /// <param name="uri">The URI to parse</param>
    /// <param name="consume">A boolean indicating whether events are consumed from the address, rather than delivered to it</param>
    /// <param name="address">The parsed address, if valid</param>
    /// <param name="error">Why the URI is not a valid address, if it is not</param>
    /// <returns>A boolean indicating whether the URI is a valid address</returns>
    public static bool TryParse(Uri? uri, bool consume, [NotNullWhen(true)] out TransportAddress? address, [NotNullWhen(false)] out string? error)
    {
        address = null;
        if (uri == null || !uri.IsAbsoluteUri)
        {
            error = "The address must be an absolute URI";
            return false;
        }
        var supported = consume ? TransportSchemes.Sources : TransportSchemes.Sinks.Where(s => s is not TransportSchemes.Http and not TransportSchemes.Https).ToList();
        if (!supported.Contains(uri.Scheme))
        {
            error = $"The scheme '{uri.Scheme}' is not supported: use one of {string.Join(", ", supported)}";
            return false;
        }
        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            error = $"The address must not contain credentials, which anyone allowed to read the resource could see: configure how to authenticate in the endpoint instead, or use a sink or a feed";
            return false;
        }
        if (string.IsNullOrWhiteSpace(uri.Host))
        {
            error = "The address must have a host";
            return false;
        }
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=');
            var name = Uri.UnescapeDataString(separator < 0 ? pair : pair[..separator]);
            parameters[name] = separator < 0 ? string.Empty : Uri.UnescapeDataString(pair[(separator + 1)..].Replace('+', ' '));
        }
        var parsed = new TransportAddress(uri, uri.Scheme, parameters);
        error = uri.Scheme switch
        {
            TransportSchemes.Kafka when !KafkaTopicPattern.IsMatch(parsed.Path) => $"The path must be the name of a Kafka topic, made of letters, digits, dots, dashes and underscores, not '{parsed.Path}'",
            TransportSchemes.Kafka when parsed.GetParameter("mechanism") is { } mechanism && mechanism.ToLowerInvariant() is not ("plain" or "scram-sha-256" or "scram-sha-512") => $"The SASL mechanism '{mechanism}' is not supported: use plain, scram-sha-256 or scram-sha-512",
            TransportSchemes.Nats when string.IsNullOrEmpty(parsed.Path) => "The path must be a NATS subject",
            TransportSchemes.Nats when !(consume ? NatsSubscriptionPattern : NatsSubjectPattern).IsMatch(parsed.Path) => consume
                ? $"'{parsed.Path}' is not a valid NATS subject: its tokens are separated by dots, and may be '*', or '>' for the last one"
                : $"'{parsed.Path}' is not a valid NATS subject to publish to: its tokens are separated by dots, and cannot be wildcards",
            TransportSchemes.Amqp or TransportSchemes.Amqps when consume && parsed.GetParameter("queue") == null => "The 'queue' query parameter must name the queue to consume",
            TransportSchemes.Amqp or TransportSchemes.Amqps when !consume && !parsed.Parameters.ContainsKey("exchange") => "The 'exchange' query parameter must name the exchange to publish to, or be empty for the default exchange, in which case 'routingKey' names the queue",
            TransportSchemes.Amqp or TransportSchemes.Amqps when !consume && string.IsNullOrEmpty(parsed.Parameters["exchange"]) && parsed.GetParameter("routingKey") == null => "The 'routingKey' query parameter must name the queue to publish to, through the default exchange",
            _ => null
        };
        if (error != null) return false;
        address = parsed;
        return true;
    }

    /// <summary>
    /// Validates the specified URI as an address cloud events can be delivered to, either over HTTP or over another transport
    /// </summary>
    /// <param name="uri">The URI to validate</param>
    /// <param name="error">Why the URI is not valid, if it is not</param>
    /// <returns>A boolean indicating whether the URI is a valid address</returns>
    public static bool IsValidSink(Uri? uri, [NotNullWhen(false)] out string? error)
    {
        if (uri != null && uri.IsAbsoluteUri && (uri.Scheme == TransportSchemes.Http || uri.Scheme == TransportSchemes.Https))
        {
            error = null;
            return true;
        }
        if (uri != null && uri.IsAbsoluteUri && IsTransport(uri)) return TryParse(uri, false, out _, out error);
        error = $"It must be an absolute URI, whose scheme is one of {string.Join(", ", TransportSchemes.Sinks)}";
        return false;
    }

    int DefaultPort() => this.Scheme switch
    {
        TransportSchemes.Kafka => 9092,
        TransportSchemes.Nats => 4222,
        TransportSchemes.Amqp => 5672,
        TransportSchemes.Amqps => 5671,
        TransportSchemes.CloudStreams => this.Tls ? 443 : 80,
        _ => -1
    };

    /// <inheritdoc/>
    public override string ToString() => this.Uri.ToString();

}
