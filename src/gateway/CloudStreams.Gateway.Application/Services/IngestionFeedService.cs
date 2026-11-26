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
using CloudStreams.Core.Transports.Authentication;
using CloudStreams.Core.Transports.Services;
using CloudStreams.Gateway.Application.Commands.CloudEvents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Neuroglia.Data.Infrastructure.ResourceOriented;
using Neuroglia.Mediation;
using Neuroglia.Serialization;

namespace CloudStreams.Gateway.Application.Services;

/// <summary>
/// Consumes cloud events from the feeds the gateway's ingestion policy lists, such as Kafka topics, NATS subjects or AMQP queues, and ingests them
/// like published ones (see #218). The cloud events of a feed belong to the namespace of its gateway (see #215)
/// </summary>
/// <param name="logger">The service used to perform logging</param>
/// <param name="serviceProvider">The current <see cref="IServiceProvider"/></param>
/// <param name="configuration">The service that holds the configuration of the gateway</param>
/// <param name="consumers">The transports cloud events can be consumed from</param>
/// <param name="serializer">The service used to deserialize cloud events</param>
/// <remarks>
/// <para>The feeds are reconciled with the configuration of the gateways, and with the <see cref="Feed"/> resources they refer to, periodically: added feeds are
/// consumed, removed ones are not anymore, and changed ones are consumed anew. A feed whose consumption fails, e.g. because its broker is unreachable, its resource
/// does not exist, or its secret cannot be read, is consumed again after a delay that grows up to a minute, with credentials read anew.</para>
/// <para>A message is acknowledged once its cloud events have been stored, ignored or refused. It is consumed again if one of them could not be stored for a reason
/// that may pass, i.e. a rate limit or an unavailable event store, so that no cloud event is lost. A message that is not a cloud event is acknowledged, and logged,
/// since consuming it again would not change that.</para>
/// <para>The replicas of the gateway share the consumption of its feeds, as a Kafka consumer group, a NATS queue group or competing AMQP consumers named after
/// the gateway and its namespace.</para>
/// </remarks>
public class IngestionFeedService(ILogger<IngestionFeedService> logger, IServiceProvider serviceProvider, IGatewayConfiguration configuration,
    IEnumerable<ICloudEventFeedConsumer> consumers, IJsonSerializer serializer)
    : BackgroundService
{

    /// <summary>
    /// Gets the prefix of the name of the publisher of the cloud events consumed from a feed, which rate limits partitioned by publisher apply to
    /// </summary>
    public const string PublisherPrefix = "feed:";

    static readonly TimeSpan MaxRestartDelay = TimeSpan.FromMinutes(1);

    readonly Dictionary<string, RunningFeed> _feeds = [];

    /// <summary>
    /// Gets/sets how often the feeds are reconciled with the configuration of the gateways
    /// </summary>
    public TimeSpan ReconciliationInterval { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets the feeds being consumed, as <c>{namespace}/{gateway}/{feed}</c>
    /// </summary>
    public IReadOnlyCollection<string> Feeds
    {
        get
        {
            lock (this._feeds) return [.. this._feeds.Keys];
        }
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await this.ReconcileAsync(configuration.Gateway is { } gateway ? [gateway] : [], stoppingToken).ConfigureAwait(false);
                await Task.Delay(this.ReconciliationInterval, stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        finally
        {
            List<RunningFeed> running;
            lock (this._feeds)
            {
                running = [.. this._feeds.Values];
                this._feeds.Clear();
            }
            foreach (var feed in running) await feed.StopAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Starts consuming the feeds of the specified gateways, and stops consuming the others
    /// </summary>
    /// <param name="gateways">The gateways whose feeds to consume</param>
    /// <param name="stoppingToken">A <see cref="CancellationToken"/> that stops the consumption of every feed</param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    protected virtual async Task ReconcileAsync(IReadOnlyCollection<Core.Resources.Gateway> gateways, CancellationToken stoppingToken)
    {
        var resolver = serviceProvider.GetRequiredService<IEndpointResolver>();
        var desired = new Dictionary<string, (FeedTarget Target, Endpoint Endpoint)>();
        foreach (var gateway in gateways)
        {
            var @namespace = gateway.GetNamespace() ?? Namespace.DefaultNamespaceName;
            foreach (var feed in gateway.Spec.Ingestion?.Feeds ?? [])
            {
                if (feed?.EffectiveName is not { } feedName) continue;
                var target = new FeedTarget(@namespace, gateway.GetName(), feedName);
                if (desired.ContainsKey(target.Key)) continue;
                // a feed refers to a Feed resource of the gateway's namespace
                try { desired[target.Key] = (target, (await resolver.GetFeedAsync(feed, @namespace, stoppingToken).ConfigureAwait(false)).Endpoint); }
                catch (InvalidOperationException ex)
                {
                    // a feed that cannot be resolved for now, e.g. one whose resource has been deleted, keeps being consumed as it was, if it was
                    lock (this._feeds) if (this._feeds.TryGetValue(target.Key, out var running)) desired[target.Key] = (target, running.Endpoint);
                    logger.LogWarning("Cannot resolve the feed '{feed}': {reason}", target.Key, ex.Message);
                }
            }
        }
        lock (this._feeds)
        {
            foreach (var (key, running) in this._feeds.ToList())
            {
                if (desired.TryGetValue(key, out var wanted) && Fingerprint(wanted.Endpoint) == running.Fingerprint) continue;
                logger.LogInformation("Stopped consuming the feed '{feed}' ({uri})", key, running.Endpoint.Uri);
                this._feeds.Remove(key);
                _ = running.StopAsync();
            }
            foreach (var (key, (target, endpoint)) in desired.Where(d => !this._feeds.ContainsKey(d.Key)))
            {
                var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                this._feeds[key] = new(key, endpoint, Fingerprint(endpoint), cancellationTokenSource, this.ConsumeAsync(target, endpoint, cancellationTokenSource.Token));
            }
        }
    }

    /// <summary>
    /// Consumes the specified feed until cancelled, consuming it again whenever its consumption fails
    /// </summary>
    /// <param name="target">The feed to consume</param>
    /// <param name="endpoint">The endpoint of the feed</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> that stops the consumption</param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    protected virtual async Task ConsumeAsync(FeedTarget target, Endpoint endpoint, CancellationToken cancellationToken)
    {
        await Task.Yield();
        var name = target.Key;
        if (!TransportAddress.TryParse(endpoint.Uri, true, out var address, out var error))
        {
            logger.LogError("Cannot consume the feed '{feed}': {reason}", name, error);
            return;
        }
        var consumer = consumers.FirstOrDefault(c => c.Schemes.Contains(address.Scheme));
        if (consumer == null)
        {
            logger.LogError("Cannot consume the feed '{feed}': the gateway does not support the '{scheme}' transport", name, address.Scheme);
            return;
        }
        var delay = TimeSpan.FromSeconds(1);
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // credentials are resolved anew whenever the consumption starts, so that a rotated secret is picked up when the previous one stops working
                var credentials = endpoint.Authentication == null ? null : await serviceProvider.GetRequiredService<ICredentialResolver>().ResolveAsync(endpoint.Authentication, target.Namespace, cancellationToken).ConfigureAwait(false);
                logger.LogInformation("Consuming the feed '{feed}' ({uri})", name, endpoint.Uri);
                await consumer.ConsumeAsync(address, credentials, target.Consumer, target.Feed, (message, token) => this.HandleAsync(target, message, token), cancellationToken).ConfigureAwait(false);
                delay = TimeSpan.FromSeconds(1);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                logger.LogError("Failed to consume the feed '{feed}' ({uri}), which will be consumed again in {delay}: {reason}", name, endpoint.Uri, delay, ex.Message);
            }
            try { await Task.Delay(delay, cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
            delay = TimeSpan.FromTicks(Math.Min(delay.Ticks * 2, MaxRestartDelay.Ticks));
        }
    }

    /// <summary>
    /// Ingests the cloud events the specified message carries
    /// </summary>
    /// <param name="target">The feed the message has been consumed from</param>
    /// <param name="message">The message to handle</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A boolean indicating whether the message should be acknowledged, which it should not if one of its cloud events could not be stored for now</returns>
    protected virtual async Task<bool> HandleAsync(FeedTarget target, TransportMessage message, CancellationToken cancellationToken)
    {
        var feed = target.Key;
        // the gateway's current policies apply: a gateway that is not served anymore stores nothing, and its feeds are stopped shortly
        var gateway = configuration.Gateway;
        if (gateway == null || gateway.GetName() != target.Gateway || gateway.GetNamespace() != target.Namespace)
        {
            logger.LogWarning("Cannot ingest {message} of feed '{feed}': its gateway has changed", message.Description, feed);
            return false;
        }
        IReadOnlyList<CloudEvent> events;
        try { events = this.Decode(message); }
        catch (FormatException ex)
        {
            logger.LogWarning("Ignored {message} of feed '{feed}', which is not a cloud event: {reason}", message.Description, feed, ex.Message);
            return true;
        }
        using var scope = serviceProvider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        foreach (var e in events)
        {
            var result = await mediator.ExecuteAsync(new ConsumeEventCommand(e, gateway, null, PublisherPrefix + target.Feed), cancellationToken).ConfigureAwait(false);
            if (result.Status is >= 200 and < 300) continue;
            var problem = result.Errors?.FirstOrDefault();
            var reason = problem?.Detail ?? problem?.Title ?? $"status {result.Status}";
            if (result.Status is (int)HttpStatusCode.TooManyRequests or >= 500)
            {
                logger.LogWarning("The cloud event '{id}' of {message} of feed '{feed}' could not be stored, and will be consumed again: {reason}", e.Id, message.Description, feed, reason);
                return false;
            }
            logger.LogWarning("The cloud event '{id}' of {message} of feed '{feed}' has been refused: {reason}", e.Id, message.Description, feed, reason);
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

    /// <summary>
    /// Gets a value that changes whenever the specified endpoint does, so that a changed feed is consumed anew
    /// </summary>
    string Fingerprint(Endpoint endpoint) => serializer.SerializeToText(endpoint);

    /// <summary>
    /// Represents a feed of a gateway
    /// </summary>
    /// <param name="Namespace">The namespace of the gateway, which the cloud events of the feed belong to</param>
    /// <param name="Gateway">The name of the gateway</param>
    /// <param name="Feed">The name of the feed, which identifies it as the publisher of its cloud events</param>
    protected sealed record FeedTarget(string Namespace, string Gateway, string Feed)
    {

        /// <summary>
        /// Gets the key that identifies the feed
        /// </summary>
        public string Key => $"{this.Namespace}/{this.Gateway}/{this.Feed}";

        /// <summary>
        /// Gets the name of the consumer group, queue group or consumer the feed is consumed as, shared by the replicas of every application that serves the gateway
        /// </summary>
        public string Consumer => $"{this.Gateway}.{this.Namespace}";

    }

    sealed record RunningFeed(string Name, Endpoint Endpoint, string Fingerprint, CancellationTokenSource CancellationTokenSource, Task Task)
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
