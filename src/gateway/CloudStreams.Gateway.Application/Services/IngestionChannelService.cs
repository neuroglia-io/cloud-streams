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

using CloudStreams.Core;
using CloudStreams.Core.Resources;
using CloudStreams.Core.Transports;
using CloudStreams.Core.Transports.Services;
using CloudStreams.Gateway.Application.Commands.CloudEvents;
using CloudStreams.Gateway.Application.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Neuroglia.Mediation;
using Neuroglia.Serialization;

namespace CloudStreams.Gateway.Application.Services;

/// <summary>
/// Consumes cloud events from the channels the gateway's ingestion policy lists, such as Kafka topics, NATS subjects or AMQP queues, and ingests them like published
/// ones (see #218)
/// </summary>
/// <param name="logger">The service used to perform logging</param>
/// <param name="serviceProvider">The current <see cref="IServiceProvider"/></param>
/// <param name="admissionControl">The service that holds the gateway's configuration</param>
/// <param name="consumers">The transports cloud events can be consumed from</param>
/// <param name="serializer">The service used to deserialize cloud events</param>
/// <param name="options">The service used to access the current <see cref="GatewayOptions"/></param>
/// <remarks>
/// <para>The channels are reconciled with the gateway's configuration periodically: added channels are consumed, removed ones are not anymore, and changed ones are
/// consumed anew. A channel whose consumption fails, e.g. because its broker is unreachable, is consumed again after a delay that grows up to a minute.</para>
/// <para>A message is acknowledged once its cloud events have been stored, ignored or refused. It is consumed again if one of them could not be stored for a reason
/// that may pass, i.e. a rate limit or an unavailable event store, so that no cloud event is lost. A message that is not a cloud event is acknowledged, and logged,
/// since consuming it again would not change that.</para>
/// </remarks>
public class IngestionChannelService(ILogger<IngestionChannelService> logger, IServiceProvider serviceProvider, ICloudEventAdmissionControl admissionControl,
    IEnumerable<ICloudEventChannelConsumer> consumers, IJsonSerializer serializer, IOptions<GatewayOptions> options)
    : BackgroundService
{

    /// <summary>
    /// Gets the prefix of the name of the publisher of the cloud events consumed from a channel, which rate limits partitioned by publisher apply to
    /// </summary>
    public const string PublisherPrefix = "channel:";

    static readonly TimeSpan MaxRestartDelay = TimeSpan.FromMinutes(1);

    readonly Dictionary<string, RunningChannel> _channels = [];

    /// <summary>
    /// Gets/sets how often the channels are reconciled with the gateway's configuration
    /// </summary>
    public TimeSpan ReconciliationInterval { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets the names of the channels being consumed
    /// </summary>
    public IReadOnlyCollection<string> Channels
    {
        get
        {
            lock (this._channels) return [.. this._channels.Keys];
        }
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                this.Reconcile(admissionControl.Gateway?.Spec.Ingestion?.Channels ?? [], stoppingToken);
                await Task.Delay(this.ReconciliationInterval, stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        finally
        {
            List<RunningChannel> running;
            lock (this._channels)
            {
                running = [.. this._channels.Values];
                this._channels.Clear();
            }
            foreach (var channel in running) await channel.StopAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Starts consuming the specified channels, and stops consuming the others
    /// </summary>
    /// <param name="channels">The channels to consume</param>
    /// <param name="stoppingToken">A <see cref="CancellationToken"/> that stops the consumption of every channel</param>
    protected virtual void Reconcile(IReadOnlyCollection<CloudEventIngestionChannel> channels, CancellationToken stoppingToken)
    {
        lock (this._channels)
        {
            foreach (var (name, running) in this._channels.ToList())
            {
                if (channels.Any(c => c.Name == name && c.Uri == running.Channel.Uri)) continue;
                logger.LogInformation("Stopped consuming the channel '{channel}' ({uri})", name, running.Channel.Uri);
                this._channels.Remove(name);
                _ = running.StopAsync();
            }
            foreach (var channel in channels.Where(c => !this._channels.ContainsKey(c.Name)))
            {
                var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                this._channels[channel.Name] = new(channel, cancellationTokenSource, this.ConsumeAsync(channel, cancellationTokenSource.Token));
            }
        }
    }

    /// <summary>
    /// Consumes the specified channel until cancelled, consuming it again whenever its consumption fails
    /// </summary>
    /// <param name="channel">The channel to consume</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> that stops the consumption</param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    protected virtual async Task ConsumeAsync(CloudEventIngestionChannel channel, CancellationToken cancellationToken)
    {
        await Task.Yield();
        if (!TransportAddress.TryParse(channel.Uri, true, out var address, out var error))
        {
            logger.LogError("Cannot consume the channel '{channel}': {reason}", channel.Name, error);
            return;
        }
        var consumer = consumers.FirstOrDefault(c => c.Schemes.Contains(address.Scheme));
        if (consumer == null)
        {
            logger.LogError("Cannot consume the channel '{channel}': the gateway does not support the '{scheme}' transport", channel.Name, address.Scheme);
            return;
        }
        var delay = TimeSpan.FromSeconds(1);
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                logger.LogInformation("Consuming the channel '{channel}' ({uri})", channel.Name, channel.Uri);
                await consumer.ConsumeAsync(address, options.Value.Name, channel.Name, (message, token) => this.HandleAsync(channel, message, token), cancellationToken).ConfigureAwait(false);
                delay = TimeSpan.FromSeconds(1);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                logger.LogError("Failed to consume the channel '{channel}' ({uri}), which will be consumed again in {delay}: {reason}", channel.Name, channel.Uri, delay, ex.Message);
            }
            try { await Task.Delay(delay, cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
            delay = TimeSpan.FromTicks(Math.Min(delay.Ticks * 2, MaxRestartDelay.Ticks));
        }
    }

    /// <summary>
    /// Ingests the cloud events the specified message carries
    /// </summary>
    /// <param name="channel">The channel the message has been consumed from</param>
    /// <param name="message">The message to handle</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A boolean indicating whether the message should be acknowledged, which it should not if one of its cloud events could not be stored for now</returns>
    protected virtual async Task<bool> HandleAsync(CloudEventIngestionChannel channel, TransportMessage message, CancellationToken cancellationToken)
    {
        IReadOnlyList<CloudEvent> events;
        try { events = this.Decode(message); }
        catch (FormatException ex)
        {
            logger.LogWarning("Ignored {message} of channel '{channel}', which is not a cloud event: {reason}", message.Description, channel.Name, ex.Message);
            return true;
        }
        using var scope = serviceProvider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        foreach (var e in events)
        {
            var result = await mediator.ExecuteAsync(new ConsumeEventCommand(e, null, PublisherPrefix + channel.Name), cancellationToken).ConfigureAwait(false);
            if (result.Status is >= 200 and < 300) continue;
            var problem = result.Errors?.FirstOrDefault();
            var reason = problem?.Detail ?? problem?.Title ?? $"status {result.Status}";
            if (result.Status is (int)HttpStatusCode.TooManyRequests or >= 500)
            {
                logger.LogWarning("The cloud event '{id}' of {message} of channel '{channel}' could not be stored, and will be consumed again: {reason}", e.Id, message.Description, channel.Name, reason);
                return false;
            }
            logger.LogWarning("The cloud event '{id}' of {message} of channel '{channel}' has been refused: {reason}", e.Id, message.Description, channel.Name, reason);
        }
        return true;
    }

    /// <summary>
    /// Decodes the cloud events the specified message carries: one in the structured or the binary content mode, or a batch
    /// </summary>
    /// <param name="message">The message to decode</param>
    /// <returns>The cloud events the message carries</returns>
    /// <exception cref="FormatException">The message is not a cloud event, nor a batch of cloud events</exception>
    protected virtual IReadOnlyList<CloudEvent> Decode(TransportMessage message)
    {
        var mode = CloudEventHttpBinding.GetContentMode(message.ContentType, message.Headers.Any(h => string.Equals(h.Key, CloudEventHttpBinding.SpecVersionHeader, StringComparison.OrdinalIgnoreCase)));
        // a message without a content type that looks like a structured cloud event is one, since some transports carry no content type
        if (mode == CloudEventContentMode.Unsupported && message.ContentType == null && message.Body.Span.TrimStart(" \t\r\n"u8).StartsWith("{"u8)) mode = CloudEventContentMode.Structured;
        return mode switch
        {
            CloudEventContentMode.Structured => [CloudEventHttpBinding.ReadStructured(message.Body, serializer)],
            CloudEventContentMode.Batch => [.. CloudEventHttpBinding.ReadBatch(message.Body).Select((node, index) => CloudEventHttpBinding.ReadEvent(node, serializer, $"The cloud event at index {index}"))],
            CloudEventContentMode.Binary => [CloudEventHttpBinding.ReadBinary(message.ContentType, message.Headers, message.Body, serializer)],
            _ => throw new FormatException($"The content type '{message.ContentType}' is not that of a cloud event, nor is the message in the binary content mode, which requires the '{CloudEventHttpBinding.SpecVersionHeader}' header")
        };
    }

    sealed record RunningChannel(CloudEventIngestionChannel Channel, CancellationTokenSource CancellationTokenSource, Task Task)
    {

        public async Task StopAsync()
        {
            await this.CancellationTokenSource.CancelAsync().ConfigureAwait(false);
            try { await this.Task.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
            finally { this.CancellationTokenSource.Dispose(); }
        }

    }

}
