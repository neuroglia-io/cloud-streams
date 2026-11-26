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
using Confluent.Kafka;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using Neuroglia.Data.Infrastructure.ResourceOriented;
using Neuroglia.Eventing.CloudEvents;
using RabbitMQ.Client;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;
using Xunit.Abstractions;

namespace CloudStreams.IntegrationTests.Cases;

/// <summary>
/// Delivers cloud events to, and consumes cloud events from, Kafka, NATS JetStream and RabbitMQ, and bridges two gateways, end to end (#218)
/// </summary>
[Collection(CloudStreamsCollection.Name)]
public sealed class TransportTests(CloudStreamsFixture fixture, ITestOutputHelper output)
    : IAsyncLifetime
{

    const string GatewayName = "gateway-e2e";

    static readonly TimeSpan Timeout = TimeSpan.FromSeconds(90);

    IReadOnlyList<int>? _logs;

    public Task InitializeAsync()
    {
        this._logs = fixture.CheckpointLogs();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        // every test that consumes a channel removes it, but a failed one may not have
        await this.SetFeedAsync(null);
        output.WriteLine(fixture.GetLogs(this._logs));
    }

    static string Id([CallerMemberName] string test = "") => $"{test.ToLowerInvariant().Replace('_', '-')[..Math.Min(test.Length, 40)].TrimEnd('-')}-{Guid.NewGuid():N}"[..56];

    async Task<Subscription> SubscribeAsync(string type, Endpoint? endpoint, CloudEventMutation? mutation = null, string? sink = null) => await fixture.Api.Resources.Subscriptions.CreateAsync(new Subscription()
    {
        Metadata = new() { Name = $"transport-{Guid.NewGuid():N}"[..40] },
        Spec = new()
        {
            Partition = new() { Type = CloudEventPartitionType.ByType, Id = type },
            Stream = new() { Offset = 0 },
            Subscriber = new() { Endpoint = endpoint, Sink = sink, RetryPolicy = new() { BackoffDuration = RetryBackoffDuration.Constant(Duration.FromSeconds(1)), MaxAttempts = 20 } },
            Mutation = mutation
        }
    });

    async Task PublishAsync(string type, params string[] ids)
    {
        foreach (var id in ids)
        {
            using var response = await fixture.Gateway.PostAsync("api/events/pub", new StringContent(JsonSerializer.Serialize(new { specversion = "1.0", id, source = "https://e2e.cloud-streams.io/transports", type, datacontenttype = "application/json", data = new { id } }), Encoding.UTF8, CloudEventContentType.Json));
            Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        }
    }

    async Task SetFeedAsync(FeedReference? feed)
    {
        var gateway = await fixture.Api.Resources.Gateways.GetAsync(GatewayName);
        if (feed == null && gateway.Spec.Ingestion?.Feeds == null) return;
        gateway.Spec.Ingestion ??= new();
        gateway.Spec.Ingestion.Feeds = feed == null ? null : [feed];
        await fixture.Api.Resources.Gateways.UpdateAsync(gateway);
    }

    /// <summary>
    /// Waits until the stream holds the specified amount of cloud events of the specified type
    /// </summary>
    async Task<List<CloudEvent>> WaitForStoredEventsAsync(string type, int count)
    {
        using var timeout = new CancellationTokenSource(Timeout);
        while (true)
        {
            try
            {
                var events = await (await fixture.Api.CloudEvents.Stream.ReadStreamAsync(new(new PartitionReference(CloudEventPartitionType.ByType, type)), null, timeout.Token)).ToListAsync(timeout.Token);
                if (events.Count >= count) return events;
            }
            catch (Neuroglia.ProblemDetailsException ex) when (ex.Problem.Status == 404) { }
            await Task.Delay(500, timeout.Token);
        }
    }

    static void AssertDelivered(IEnumerable<(byte[] Body, string? Subscription)> messages, Subscription subscription, params string[] ids)
    {
        var delivered = messages.ToList();
        Assert.Equal(ids, delivered.Select(m => JsonNode.Parse(m.Body)!["id"]!.GetValue<string>()));
        Assert.All(delivered, m => Assert.Equal(subscription.GetName(), m.Subscription));
    }

    [Fact]
    public async Task Cloud_Events_Should_Be_Delivered_To_A_Kafka_Topic_In_Order()
    {
        // arrange
        var id = Id();
        var subscription = await this.SubscribeAsync(id, new Uri($"kafka://{fixture.KafkaServer}/{id}"));
        using var consumer = new ConsumerBuilder<string?, byte[]>(new Confluent.Kafka.ConsumerConfig() { BootstrapServers = fixture.KafkaServer, GroupId = id, AutoOffsetReset = AutoOffsetReset.Earliest }).Build();
        consumer.Subscribe(id);

        // act
        await this.PublishAsync(id, "1", "2", "3");
        var messages = new List<(byte[], string?)>();
        using var timeout = new CancellationTokenSource(Timeout);
        while (messages.Count < 3)
        {
            ConsumeResult<string?, byte[]>? result;
            // the topic only exists once the broker has delivered to it
            try { result = consumer.Consume(timeout.Token); }
            catch (ConsumeException ex) when (!ex.Error.IsFatal) { await Task.Delay(250, timeout.Token); continue; }
            if (result?.Message == null) continue;
            messages.Add((result.Message.Value, result.Message.Headers.TryGetLastBytes(CloudStreamsHeaders.Subscription, out var header) ? Encoding.UTF8.GetString(header) : null));
            Assert.Equal(CloudEventContentType.Json, Encoding.UTF8.GetString(result.Message.Headers.GetLastBytes("content-type")));
        }

        // assert
        AssertDelivered(messages, subscription, "1", "2", "3");
    }

    [Fact]
    public async Task Cloud_Events_Should_Be_Consumed_From_A_Kafka_Topic_In_The_Binary_Content_Mode()
    {
        // arrange
        var id = Id();
        await this.SetFeedAsync(new FeedReference() { Name = "kafka-e2e", Endpoint = new Uri($"kafka://{fixture.KafkaServer}/{id}") });
        using var producer = new ProducerBuilder<string?, byte[]>(new ProducerConfig() { BootstrapServers = fixture.KafkaServer }).Build();

        // act
        await producer.ProduceAsync(id, new Message<string?, byte[]>()
        {
            Value = Encoding.UTF8.GetBytes("""{ "id": "1" }"""),
            Headers = new Confluent.Kafka.Headers()
            {
                { "ce_specversion", Encoding.UTF8.GetBytes("1.0") },
                { "ce_id", Encoding.UTF8.GetBytes(id) },
                { "ce_source", Encoding.UTF8.GetBytes("https://e2e.cloud-streams.io/kafka") },
                { "ce_type", Encoding.UTF8.GetBytes(id) },
                { "content-type", Encoding.UTF8.GetBytes("application/json") }
            }
        });
        var stored = await this.WaitForStoredEventsAsync(id, 1);

        // assert
        var e = Assert.Single(stored);
        Assert.Equal(id, e.Id);
        Assert.Equal("1", JsonSerializer.SerializeToNode(e.Data)!["id"]!.GetValue<string>());
    }

    [Fact]
    public async Task Cloud_Events_Should_Be_Delivered_To_And_Consumed_From_NATS_JetStream()
    {
        // arrange
        var id = Id();
        var subject = id.Replace('-', '_');
        await using var nats = new NatsConnection(NatsOpts.Default with { Url = $"nats://{fixture.NatsServer}" });
        var jetStream = new NatsJSContext(nats);
        var stream = await jetStream.CreateStreamAsync(new StreamConfig(subject, [$"{subject}.>"]));
        var subscription = await this.SubscribeAsync(id, new Uri($"nats://{fixture.NatsServer}/{subject}.out?jetstream=true"));
        await this.SetFeedAsync(new FeedReference() { Name = "nats-e2e", Endpoint = new Uri($"nats://{fixture.NatsServer}/{subject}.in?jetstream=true") });

        // act - delivered to a subject of the stream
        await this.PublishAsync(id, "1", "2");
        var consumer = await jetStream.CreateOrUpdateConsumerAsync(subject, new NATS.Client.JetStream.Models.ConsumerConfig("test") { FilterSubject = $"{subject}.out" });
        var messages = new List<(byte[], string?)>();
        using var timeout = new CancellationTokenSource(Timeout);
        while (messages.Count < 2)
        {
            await foreach (var message in consumer.FetchAsync<byte[]>(new NatsJSFetchOpts() { MaxMsgs = 2, Expires = TimeSpan.FromSeconds(5) }, cancellationToken: timeout.Token))
            {
                messages.Add((message.Data!, message.Headers?[CloudStreamsHeaders.Subscription].ToString()));
                await message.AckAsync(cancellationToken: timeout.Token);
            }
        }

        // act - consumed from another subject of the stream, in the structured content mode
        var consumedType = $"{id}-in";
        var ack = await jetStream.PublishAsync($"{subject}.in", Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { specversion = "1.0", id = "in-1", source = "https://e2e.cloud-streams.io/nats", type = consumedType })),
            headers: new NatsHeaders() { ["content-type"] = CloudEventContentType.Json });
        ack.EnsureSuccess();
        var stored = await this.WaitForStoredEventsAsync(consumedType, 1);

        // assert
        AssertDelivered(messages, subscription, "1", "2");
        Assert.Equal("in-1", Assert.Single(stored).Id);
    }

    [Fact]
    public async Task Cloud_Events_Should_Be_Delivered_To_And_Consumed_From_RabbitMQ_With_Credentials_Read_From_A_Secret()
    {
        // arrange
        var id = Id();
        var factory = new ConnectionFactory() { Uri = new(fixture.RabbitMqConnectionString) };
        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();
        await channel.ExchangeDeclareAsync(id, ExchangeType.Fanout, autoDelete: true);
        await channel.QueueDeclareAsync($"{id}-out", false, false, true);
        await channel.QueueBindAsync($"{id}-out", id, string.Empty);
        await channel.QueueDeclareAsync($"{id}-in", false, false, true);
        var authentication = new AuthenticationPolicy() { Basic = new() { Use = new(CloudStreamsFixture.RabbitMqSecret) } };
        var subscription = await this.SubscribeAsync(id, new Endpoint(new Uri($"amqp://{fixture.RabbitMqServer}/?exchange={id}"), authentication));
        // the queue is consumed through a feed resource, which holds its endpoint and credentials
        await fixture.Api.Resources.Feeds.CreateAsync(new Feed(new(id), new() { Endpoint = new(new Uri($"amqp://{fixture.RabbitMqServer}/?queue={id}-in"), authentication) }));
        await this.SetFeedAsync(id);

        // act - delivered to an exchange
        await this.PublishAsync(id, "1", "2");
        var messages = new List<(byte[], string?)>();
        using var timeout = new CancellationTokenSource(Timeout);
        while (messages.Count < 2)
        {
            var message = await channel.BasicGetAsync($"{id}-out", true, timeout.Token);
            if (message == null)
            {
                await Task.Delay(250, timeout.Token);
                continue;
            }
            messages.Add((message.Body.ToArray(), message.BasicProperties.Headers?.TryGetValue(CloudStreamsHeaders.Subscription, out var header) == true ? Encoding.UTF8.GetString((byte[])header!) : null));
        }

        // act - consumed from a queue, in the binary content mode of the AMQP binding
        var consumedType = $"{id}-in";
        await channel.BasicPublishAsync(string.Empty, $"{id}-in", false, new BasicProperties()
        {
            ContentType = "application/json",
            Headers = new Dictionary<string, object?>()
            {
                ["cloudEvents:specversion"] = "1.0",
                ["cloudEvents:id"] = "in-1",
                ["cloudEvents:source"] = "https://e2e.cloud-streams.io/amqp",
                ["cloudEvents:type"] = consumedType
            }
        }, Encoding.UTF8.GetBytes("""{ "id": "in-1" }"""));
        var stored = await this.WaitForStoredEventsAsync(consumedType, 1);

        // assert
        AssertDelivered(messages, subscription, "1", "2");
        Assert.Equal("in-1", Assert.Single(stored).Id);
    }

    [Fact]
    public async Task Cloud_Events_Should_Be_Bridged_To_Another_Gateway()
    {
        // arrange - the gateway bridges to itself: the mutation gives bridged cloud events another type, and removes their origin, which the split-horizon policy
        // would otherwise recognize as a loop
        var id = Id();
        var bridgedType = $"{id}-bridged";
        await this.SubscribeAsync(id, new Uri($"cloudstreams://{fixture.GatewayUri.Authority}?tls=false"), new() { Type = CloudEventMutationType.Expression, Expression = $"${{ . + {{ type: \"{bridgedType}\" }} | del(.{SplitHorizonPolicy.DefaultAttributeName}) }}" });

        // act
        await this.PublishAsync(id, "1", "2");
        var bridged = await this.WaitForStoredEventsAsync(bridgedType, 2);

        // assert
        Assert.Equal(["1", "2"], bridged.Select(e => e.Id));
    }


    [Fact]
    public async Task Cloud_Events_Should_Be_Delivered_To_A_Sink_With_Basic_Credentials_Read_From_A_Secret()
    {
        // arrange - the sink holds the endpoint and its credentials, which the subscription only refers to by name
        var id = Id();
        var endpoint = fixture.Subscriber.Register(id);
        await fixture.Api.Resources.Sinks.CreateAsync(new Sink(new(id), new() { Endpoint = new(endpoint.Uri, new() { Basic = new() { Use = new(CloudStreamsFixture.RabbitMqSecret) } }) }));
        await this.SubscribeAsync(id, null, sink: id);

        // act
        await this.PublishAsync(id, "1", "2");
        var deliveries = await endpoint.WaitForAsync(2, Timeout);

        // assert
        Assert.Equal(["1", "2"], deliveries.Select(d => d.Id));
        Assert.All(deliveries, d => Assert.Equal($"Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes("cloud-streams:e2e-secret"))}", d.Authorization));
    }

    [Fact]
    public async Task Cloud_Events_Should_Be_Delivered_With_An_OAuth2_Token_Obtained_With_The_Client_Credentials()
    {
        // arrange - the test subscriber is also the authorization server
        var id = Id();
        var endpoint = fixture.Subscriber.Register(id);
        var authentication = new AuthenticationPolicy() { OAuth2 = new() { Authority = fixture.Subscriber.BaseUri, Client = new() { Id = id, Secret = "client-secret" }, Scopes = ["events"] } };
        await this.SubscribeAsync(id, new Endpoint(endpoint.Uri, authentication));

        // act
        await this.PublishAsync(id, "1");
        var delivery = Assert.Single(await endpoint.WaitForAsync(1, Timeout));

        // assert
        Assert.Equal($"Bearer {TestSubscriber.IssuedToken}", delivery.Authorization);
        var request = Assert.Single(fixture.Subscriber.TokenRequests, r => r.GetValueOrDefault("client_id") == id);
        Assert.Equal("client_credentials", request["grant_type"]);
        Assert.Equal("client-secret", request["client_secret"]);
        Assert.Equal("events", request["scope"]);
    }

    [Fact]
    public async Task Credentials_Should_Be_Read_From_Vault()
    {
        // arrange - the secret is written to Vault's KV engine, which the broker reads it from, explicitly
        var id = Id();
        var endpoint = fixture.Subscriber.Register(id);
        using var vault = new HttpClient() { BaseAddress = fixture.VaultUri };
        vault.DefaultRequestHeaders.Add("X-Vault-Token", CloudStreamsFixture.VaultToken);
        using var written = await vault.PostAsync($"v1/secret/data/{id}", new StringContent("""{ "data": { "username": "vault-user", "password": "vault-secret" } }""", Encoding.UTF8, "application/json"));
        Assert.True(written.IsSuccessStatusCode, await written.Content.ReadAsStringAsync());
        await this.SubscribeAsync(id, new Endpoint(endpoint.Uri, new() { Basic = new() { Use = new(SecretProviders.Vault, id) } }));

        // act
        await this.PublishAsync(id, "1");
        var delivery = Assert.Single(await endpoint.WaitForAsync(1, Timeout));

        // assert
        Assert.Equal($"Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes("vault-user:vault-secret"))}", delivery.Authorization);
    }


    [Fact]
    public async Task A_Cloud_Event_Should_Be_Redelivered_To_The_Sink_Of_Its_Subscription_With_Its_Credentials()
    {
        // arrange - redeliveries are made by the API, which must resolve the sink and its secret as the broker does
        var id = Id();
        var endpoint = fixture.Subscriber.Register(id);
        await fixture.Api.Resources.Sinks.CreateAsync(new Sink(new(id), new() { Endpoint = new(endpoint.Uri, new() { Bearer = new() { Token = "redelivery-token" } }) }));
        var subscription = await this.SubscribeAsync(id, null, sink: id);
        await this.PublishAsync(id, "1");
        // the subscription reads the partition of the type, whose offset the delivery carries
        var offset = ulong.Parse(Assert.Single(await endpoint.WaitForAsync(1, Timeout)).Offset, System.Globalization.CultureInfo.InvariantCulture);

        // act
        var redelivery = await fixture.Api.Resources.Subscriptions.RedeliverAsync(subscription.GetName(), new() { Partition = new(CloudEventPartitionType.ByType, id), Offset = offset });
        var deliveries = await endpoint.WaitForAsync(2, Timeout);

        // assert
        Assert.Equal("1", redelivery.Id);
        Assert.All(deliveries, d => Assert.Equal("Bearer redelivery-token", d.Authorization));
    }

}
