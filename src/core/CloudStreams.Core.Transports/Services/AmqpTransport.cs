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

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Neuroglia.Eventing.CloudEvents;
using Neuroglia.Serialization;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using System.Collections.Concurrent;

namespace CloudStreams.Core.Transports.Services;

/// <summary>
/// Delivers cloud events to AMQP 0-9-1 exchanges, such as RabbitMQ's, and consumes cloud events from their queues, following the CloudEvents AMQP protocol binding (see #218)
/// </summary>
/// <param name="logger">The service used to perform logging</param>
/// <param name="options">The service used to access the current <see cref="TransportOptions"/></param>
/// <param name="serializer">The service used to serialize cloud events</param>
/// <remarks>
/// Cloud events are published as persistent messages, with publisher confirms, and as mandatory, so that a cloud event no queue is bound to receive is not delivered,
/// rather than silently dropped. Consumers acknowledge a message once its cloud event has been handled, and requeue it otherwise
/// </remarks>
public sealed class AmqpTransport(ILogger<AmqpTransport> logger, IOptionsMonitor<TransportOptions> options, IJsonSerializer serializer)
    : ICloudEventSink, ICloudEventChannelConsumer, IAsyncDisposable
{

    const ushort Prefetch = 16;

    readonly ConcurrentDictionary<string, Lazy<Task<IConnection>>> _connections = new();

    /// <inheritdoc/>
    public IReadOnlyList<string> Schemes { get; } = [TransportSchemes.Amqp, TransportSchemes.Amqps];

    /// <inheritdoc/>
    public async Task SendAsync(TransportAddress address, IReadOnlyList<CloudEvent> events, IReadOnlyList<KeyValuePair<string, string>> headers, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(address);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(headers);
        var connection = await this.GetConnectionAsync(address, cancellationToken).ConfigureAwait(false);
        await using var channel = await connection.CreateChannelAsync(new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true), cancellationToken).ConfigureAwait(false);
        var exchange = address.Parameters.TryGetValue("exchange", out var name) ? name : string.Empty;
        foreach (var e in events)
        {
            var properties = new BasicProperties()
            {
                ContentType = CloudEventContentType.Json,
                MessageId = e.Id,
                Persistent = true,
                Headers = headers.ToDictionary(h => h.Key, h => (object?)h.Value)
            };
            try
            {
                await channel.BasicPublishAsync(exchange, address.GetParameter("routingKey") ?? e.Type, true, properties, TransportMessageEncoding.Encode(e, serializer), cancellationToken).ConfigureAwait(false);
            }
            catch (PublishException ex)
            {
                throw new InvalidOperationException(ex.IsReturn
                    ? $"The exchange '{exchange}' could not route the cloud event to any queue: bind a queue to it, or set the 'routingKey' that a bound queue expects"
                    : $"The broker did not accept the cloud event: {ex.Message}", ex);
            }
        }
    }

    /// <inheritdoc/>
    public async Task ConsumeAsync(TransportAddress address, string group, string name, Func<TransportMessage, CancellationToken, Task<bool>> handler, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(address);
        ArgumentNullException.ThrowIfNull(handler);
        var queue = address.GetParameter("queue")!;
        // consumers have a connection of their own, so that their flow control does not slow down publishing
        await using var connection = await this.CreateFactory(address).CreateConnectionAsync($"cloud-streams-{group}", cancellationToken).ConfigureAwait(false);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        await channel.BasicQosAsync(0, Prefetch, false, cancellationToken).ConfigureAwait(false);
        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        channel.ChannelShutdownAsync += (_, e) =>
        {
            // a shutdown initiated by the server, e.g. because the queue has been deleted, stops the consumption, so that it is restarted
            if (e.Initiator != ShutdownInitiator.Application) stopped.TrySetException(new InvalidOperationException($"The consumption of the queue '{queue}' stopped: {e.ReplyText}"));
            return Task.CompletedTask;
        };
        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, delivery) =>
        {
            var message = ToMessage(queue, delivery);
            bool handled;
            try { handled = await handler(message, cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return; }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to handle {message}", message.Description);
                handled = false;
            }
            if (handled)
            {
                await channel.BasicAckAsync(delivery.DeliveryTag, false, CancellationToken.None).ConfigureAwait(false);
                return;
            }
            // requeuing immediately would redeliver the message as fast as it fails
            try { await Task.Delay(TimeSpan.FromSeconds(delivery.Redelivered ? 5 : 1), cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) { }
            await channel.BasicNackAsync(delivery.DeliveryTag, false, true, CancellationToken.None).ConfigureAwait(false);
        };
        await channel.BasicConsumeAsync(queue, false, $"cloud-streams-{group}-{name}", consumer, cancellationToken).ConfigureAwait(false);
        using var registration = cancellationToken.Register(() => stopped.TrySetResult());
        await stopped.Task.ConfigureAwait(false);
    }

    static TransportMessage ToMessage(string queue, BasicDeliverEventArgs delivery)
    {
        var headers = new List<KeyValuePair<string, string?>>();
        foreach (var (name, value) in delivery.BasicProperties.Headers ?? new Dictionary<string, object?>())
        {
            headers.Add(new(TransportMessageEncoding.NormalizeHeader(name), TransportMessageEncoding.DecodeHeaderValue(value)));
        }
        return new(delivery.BasicProperties.ContentType, headers, delivery.Body.ToArray(), $"the message {delivery.DeliveryTag} of queue '{queue}'");
    }

    ConnectionFactory CreateFactory(TransportAddress address)
    {
        var credentials = options.CurrentValue.GetCredentials(address);
        var separator = address.Server.LastIndexOf(':');
        var factory = new ConnectionFactory()
        {
            HostName = address.Uri.Host,
            Port = int.Parse(address.Server[(separator + 1)..], System.Globalization.CultureInfo.InvariantCulture),
            VirtualHost = string.IsNullOrEmpty(address.Path) ? "/" : address.Path,
            AutomaticRecoveryEnabled = true,
            Ssl = new SslOption() { Enabled = address.Tls, ServerName = address.Uri.Host }
        };
        if (credentials?.Username != null) factory.UserName = credentials.Username;
        if (credentials?.Password != null) factory.Password = credentials.Password;
        return factory;
    }

    async Task<IConnection> GetConnectionAsync(TransportAddress address, CancellationToken cancellationToken)
    {
        var key = $"{address.Server}|{address.Path}|{address.Tls}|{address.Credentials}";
        var connection = this._connections.GetOrAdd(key, _ => new(() => this.CreateFactory(address).CreateConnectionAsync("cloud-streams", CancellationToken.None)));
        try { return await connection.Value.WaitAsync(cancellationToken).ConfigureAwait(false); }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            this._connections.TryRemove(key, out _);
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        foreach (var connection in this._connections.Values.Where(c => c.IsValueCreated && c.Value.IsCompletedSuccessfully))
        {
            await connection.Value.Result.DisposeAsync().ConfigureAwait(false);
        }
        this._connections.Clear();
    }

}
