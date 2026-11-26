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
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Neuroglia.Eventing.CloudEvents;
using Neuroglia.Serialization;
using System.Collections.Concurrent;
using System.Text;

namespace CloudStreams.Core.Transports.Services;

/// <summary>
/// Delivers cloud events to, and consumes cloud events from, Kafka topics, following the CloudEvents Kafka protocol binding (see #218)
/// </summary>
/// <param name="logger">The service used to perform logging</param>
/// <param name="serializer">The service used to serialize cloud events</param>
/// <remarks>
/// Producers wait for every in-sync replica to acknowledge a cloud event, and are idempotent, so that retries never reorder nor duplicate cloud events. Cloud events
/// are keyed by their <c>partitionkey</c>, or their subject, so that those with the same key stay in order. Consumers commit an offset once its cloud event has been
/// handled, and retry it until then, which keeps the cloud events of a partition in order
/// </remarks>
public sealed class KafkaTransport(ILogger<KafkaTransport> logger, IJsonSerializer serializer)
    : ICloudEventSink, ICloudEventFeedConsumer, IDisposable
{

    static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(30);

    readonly ConcurrentDictionary<string, Lazy<IProducer<string?, byte[]>>> _producers = new();

    /// <inheritdoc/>
    public IReadOnlyList<string> Schemes { get; } = [TransportSchemes.Kafka];

    /// <inheritdoc/>
    public async Task SendAsync(TransportAddress address, TransportCredentials? credentials, IReadOnlyList<CloudEvent> events, IReadOnlyList<KeyValuePair<string, string>> headers, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(address);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(headers);
        var producer = this.GetProducer(address, credentials);
        var deliveries = new List<Task<DeliveryResult<string?, byte[]>>>(events.Count);
        foreach (var e in events)
        {
            var message = new Message<string?, byte[]>() { Key = TransportMessageEncoding.GetKey(e), Value = TransportMessageEncoding.Encode(e, serializer), Headers = [] };
            message.Headers.Add(TransportMessageEncoding.ContentTypeHeader, Encoding.UTF8.GetBytes(CloudEventContentType.Json));
            foreach (var (name, value) in headers) message.Headers.Add(name, Encoding.UTF8.GetBytes(value));
            deliveries.Add(producer.ProduceAsync(address.Path, message, cancellationToken));
        }
        try { await Task.WhenAll(deliveries).ConfigureAwait(false); }
        catch (ProduceException<string?, byte[]> ex) { throw new KafkaException(new Error(ex.Error.Code, $"Kafka did not accept the cloud event for topic '{address.Path}': {ex.Error.Reason}")); }
    }

    /// <inheritdoc/>
    public Task ConsumeAsync(TransportAddress address, TransportCredentials? credentials, string group, string name, Func<TransportMessage, CancellationToken, Task<bool>> handler, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(address);
        ArgumentException.ThrowIfNullOrWhiteSpace(group);
        ArgumentNullException.ThrowIfNull(handler);
        var configuration = new ConsumerConfig(CreateConfiguration(address, credentials))
        {
            GroupId = address.GetParameter("group") ?? group,
            EnableAutoCommit = false,
            AutoOffsetReset = string.Equals(address.GetParameter("offset"), "latest", StringComparison.OrdinalIgnoreCase) ? AutoOffsetReset.Latest : AutoOffsetReset.Earliest
        };
        // the consumer's API is blocking: it runs on a thread of its own
        return Task.Factory.StartNew(async () =>
        {
            using var consumer = new ConsumerBuilder<string?, byte[]>(configuration)
                .SetErrorHandler((_, error) => logger.LogWarning("The Kafka consumer of '{address}' reported an error: {reason}", address.Uri, error.Reason))
                .SetOAuthBearerTokenRefreshHandler((client, _) => RefreshToken(client, credentials))
                .Build();
            consumer.Subscribe(address.Path);
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    ConsumeResult<string?, byte[]>? result;
                    try { result = consumer.Consume(cancellationToken); }
                    catch (OperationCanceledException) { break; }
                    catch (ConsumeException ex) when (!ex.Error.IsFatal)
                    {
                        logger.LogWarning("Failed to consume '{address}': {reason}", address.Uri, ex.Error.Reason);
                        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
                        continue;
                    }
                    if (result?.Message == null || result.IsPartitionEOF) continue;
                    var message = ToMessage(result);
                    var delay = TimeSpan.FromSeconds(1);
                    while (!await handler(message, cancellationToken).ConfigureAwait(false))
                    {
                        await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                        delay = TimeSpan.FromTicks(Math.Min(delay.Ticks * 2, MaxRetryDelay.Ticks));
                    }
                    consumer.Commit(result);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
            finally
            {
                consumer.Close();
            }
        }, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();
    }

    static TransportMessage ToMessage(ConsumeResult<string?, byte[]> result)
    {
        string? contentType = null;
        var headers = new List<KeyValuePair<string, string?>>();
        foreach (var header in result.Message.Headers ?? [])
        {
            var value = header.GetValueBytes() is { } bytes ? Encoding.UTF8.GetString(bytes) : null;
            if (string.Equals(header.Key, TransportMessageEncoding.ContentTypeHeader, StringComparison.OrdinalIgnoreCase)) contentType = value;
            else headers.Add(new(TransportMessageEncoding.NormalizeHeader(header.Key), value));
        }
        return new(contentType, headers, result.Message.Value ?? [], $"the message at offset {result.Offset.Value} of partition {result.Partition.Value} of topic '{result.Topic}'");
    }

    IProducer<string?, byte[]> GetProducer(TransportAddress address, TransportCredentials? credentials)
    {
        var key = $"{address.Server}|{address.GetParameter("servers")}|{address.Tls}|{address.GetParameter("mechanism")}|{credentials?.Fingerprint}";
        var producer = this._producers.GetOrAdd(key, _ => new(() => new ProducerBuilder<string?, byte[]>(new ProducerConfig(CreateConfiguration(address, credentials))
        {
            Acks = Acks.All,
            EnableIdempotence = true,
            ClientId = "cloud-streams"
        }).SetErrorHandler((_, error) => logger.LogWarning("The Kafka producer of '{servers}' reported an error: {reason}", address.Server, error.Reason))
            .SetOAuthBearerTokenRefreshHandler((client, _) => RefreshToken(client, credentials)).Build()));
        try { return producer.Value; }
        catch
        {
            this._producers.TryRemove(key, out _);
            throw;
        }
    }

    static ClientConfig CreateConfiguration(TransportAddress address, TransportCredentials? credentials)
    {
        var servers = new List<string>() { address.Server };
        if (address.GetParameter("servers") is { } additional) servers.AddRange(additional.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        var configuration = new ClientConfig()
        {
            BootstrapServers = string.Join(',', servers),
            SecurityProtocol = (address.Tls, credentials != null) switch
            {
                (true, true) => SecurityProtocol.SaslSsl,
                (true, false) => SecurityProtocol.Ssl,
                (false, true) => SecurityProtocol.SaslPlaintext,
                _ => SecurityProtocol.Plaintext
            }
        };
        if (credentials?.IsToken == true) configuration.SaslMechanism = SaslMechanism.OAuthBearer;
        else if (credentials != null)
        {
            configuration.SaslMechanism = address.GetParameter("mechanism")?.ToLowerInvariant() switch
            {
                "scram-sha-256" => SaslMechanism.ScramSha256,
                "scram-sha-512" => SaslMechanism.ScramSha512,
                _ => SaslMechanism.Plain
            };
            configuration.SaslUsername = credentials.Username;
            configuration.SaslPassword = credentials.Password;
        }
        return configuration;
    }

    /// <summary>
    /// Sets the OAUTHBEARER token of the specified client, which Kafka asks for before the previous one expires
    /// </summary>
    static void RefreshToken(IClient client, TransportCredentials? credentials)
    {
        if (credentials?.IsToken != true) return;
        try
        {
            var token = credentials.GetTokenAsync().GetAwaiter().GetResult();
            var expiresAt = token.ExpiresAt == DateTimeOffset.MaxValue ? DateTimeOffset.UtcNow.AddHours(1) : token.ExpiresAt;
            client.OAuthBearerSetToken(token.Value, expiresAt.ToUnixTimeMilliseconds(), "cloud-streams");
        }
        catch (Exception ex)
        {
            client.OAuthBearerSetTokenFailure(ex.Message);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        foreach (var producer in this._producers.Values.Where(p => p.IsValueCreated))
        {
            producer.Value.Flush(TimeSpan.FromSeconds(5));
            producer.Value.Dispose();
        }
        this._producers.Clear();
    }

}
