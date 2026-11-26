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

using CloudStreams.Core.Transports.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using Neuroglia.Eventing.CloudEvents;
using Neuroglia.Serialization;
using System.Collections.Concurrent;

namespace CloudStreams.Core.Transports.Services;

/// <summary>
/// Delivers cloud events to, and consumes cloud events from, NATS subjects, following the CloudEvents NATS protocol binding (see #218)
/// </summary>
/// <param name="logger">The service used to perform logging</param>
/// <param name="serializer">The service used to serialize cloud events</param>
/// <remarks>
/// <para>Core NATS delivers at most once: a cloud event published while no one listens is lost, and one a gateway fails to store cannot be consumed again. With
/// <c>jetstream=true</c>, publishing waits for JetStream to store the cloud event, and gateways consume it with a durable consumer, which redelivers it until it is
/// stored.</para>
/// </remarks>
public sealed class NatsTransport(ILogger<NatsTransport> logger, IJsonSerializer serializer)
    : ICloudEventSink, ICloudEventFeedConsumer, IAsyncDisposable
{

    readonly ConcurrentDictionary<string, Lazy<NatsConnection>> _connections = new();

    /// <inheritdoc/>
    public IReadOnlyList<string> Schemes { get; } = [TransportSchemes.Nats];

    static bool UsesJetStream(TransportAddress address) => string.Equals(address.GetParameter("jetstream"), "true", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public async Task SendAsync(TransportAddress address, TransportCredentials? credentials, IReadOnlyList<CloudEvent> events, IReadOnlyList<KeyValuePair<string, string>> headers, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(address);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(headers);
        var connection = await this.GetConnectionAsync(address, credentials, cancellationToken).ConfigureAwait(false);
        var jetStream = UsesJetStream(address) ? new NatsJSContext(connection) : null;
        foreach (var e in events)
        {
            var natsHeaders = new NatsHeaders() { [TransportMessageEncoding.ContentTypeHeader] = CloudEventContentType.Json };
            foreach (var (name, value) in headers) natsHeaders[name] = value;
            var body = TransportMessageEncoding.Encode(e, serializer);
            if (jetStream == null) await connection.PublishAsync(address.Path, body, natsHeaders, cancellationToken: cancellationToken).ConfigureAwait(false);
            else (await jetStream.PublishAsync(address.Path, body, headers: natsHeaders, cancellationToken: cancellationToken).ConfigureAwait(false)).EnsureSuccess();
        }
        // core NATS does not acknowledge messages: flushing at least surfaces a connection that has been lost
        if (jetStream == null) await connection.PingAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task ConsumeAsync(TransportAddress address, TransportCredentials? credentials, string group, string name, Func<TransportMessage, CancellationToken, Task<bool>> handler, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(address);
        ArgumentException.ThrowIfNullOrWhiteSpace(group);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(handler);
        var connection = await this.GetConnectionAsync(address, credentials, cancellationToken).ConfigureAwait(false);
        try
        {
            if (!UsesJetStream(address))
            {
                await foreach (var message in connection.SubscribeAsync<byte[]>(address.Path, address.GetParameter("queue") ?? group, cancellationToken: cancellationToken).ConfigureAwait(false))
                {
                    var consumed = ToMessage(message.Subject, message.Headers, message.Data, $"a message of subject '{message.Subject}'");
                    if (!await handler(consumed, cancellationToken).ConfigureAwait(false)) logger.LogWarning("Core NATS cannot redeliver {message}, whose cloud event has not been stored: consume subjects with JetStream, with 'jetstream=true', not to lose cloud events", consumed.Description);
                }
                return;
            }
            var jetStream = new NatsJSContext(connection);
            string? stream = null;
            await foreach (var streamName in jetStream.ListStreamNamesAsync(address.Path, cancellationToken).ConfigureAwait(false))
            {
                stream = streamName;
                break;
            }
            if (stream == null) throw new InvalidOperationException($"No JetStream stream captures the subject '{address.Path}': create one, or consume it without JetStream");
            var durableName = $"{group}-{name}".Replace('.', '-');
            var consumer = await jetStream.CreateOrUpdateConsumerAsync(stream, new ConsumerConfig(durableName)
            {
                FilterSubject = address.Path,
                AckPolicy = ConsumerConfigAckPolicy.Explicit,
                DeliverPolicy = string.Equals(address.GetParameter("offset"), "latest", StringComparison.OrdinalIgnoreCase) ? ConsumerConfigDeliverPolicy.New : ConsumerConfigDeliverPolicy.All
            }, cancellationToken).ConfigureAwait(false);
            await foreach (var message in consumer.ConsumeAsync<byte[]>(cancellationToken: cancellationToken).ConfigureAwait(false))
            {
                var consumed = ToMessage(message.Subject, message.Headers, message.Data, $"the message {message.Metadata?.Sequence.Stream} of stream '{stream}'");
                if (await handler(consumed, cancellationToken).ConfigureAwait(false)) await message.AckAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
                else
                {
                    var deliveries = (int)Math.Min(message.Metadata?.NumDelivered ?? 1, 5);
                    await message.NakAsync(delay: TimeSpan.FromSeconds(Math.Pow(2, deliveries)), cancellationToken: cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    }

    static TransportMessage ToMessage(string subject, NatsHeaders? headers, byte[]? data, string description)
    {
        string? contentType = null;
        var normalized = new List<KeyValuePair<string, string?>>();
        foreach (var (name, values) in headers ?? [])
        {
            var value = values.ToString();
            if (string.Equals(name, TransportMessageEncoding.ContentTypeHeader, StringComparison.OrdinalIgnoreCase)) contentType = value;
            else normalized.Add(new(TransportMessageEncoding.NormalizeHeader(name), value));
        }
        return new(contentType, normalized, data ?? [], description);
    }

    async Task<NatsConnection> GetConnectionAsync(TransportAddress address, TransportCredentials? credentials, CancellationToken cancellationToken)
    {
        var token = credentials?.IsToken == true ? (await credentials.GetTokenAsync(cancellationToken).ConfigureAwait(false)).Value : null;
        var key = $"{address.Server}|{address.Tls}|{credentials?.Fingerprint}";
        var connection = this._connections.GetOrAdd(key, _ => new(() =>
        {
            return new NatsConnection(NatsOpts.Default with
            {
                Url = $"{(address.Tls ? "tls" : "nats")}://{address.Server}",
                Name = "cloud-streams",
                AuthOpts = credentials == null ? NatsAuthOpts.Default : NatsAuthOpts.Default with
                {
                    Username = credentials.Username,
                    Password = credentials.Password,
                    Token = token
                },
                TlsOpts = address.Tls ? NatsTlsOpts.Default with { Mode = TlsMode.Require } : NatsTlsOpts.Default
            });
        }));
        try { return connection.Value; }
        catch
        {
            this._connections.TryRemove(key, out _);
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        foreach (var connection in this._connections.Values.Where(c => c.IsValueCreated)) await connection.Value.DisposeAsync().ConfigureAwait(false);
        this._connections.Clear();
    }

}
