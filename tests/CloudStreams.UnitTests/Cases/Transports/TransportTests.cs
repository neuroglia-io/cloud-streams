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

using CloudStreams.Broker.Application.Services.Delivery;
using CloudStreams.Core.Application.Validation;
using CloudStreams.Core.Transports;
using CloudStreams.Core.Transports.Services;
using CloudStreams.Gateway.Application.Commands.CloudEvents;
using CloudStreams.Gateway.Application.Configuration;
using CloudStreams.Gateway.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Neuroglia.Mediation;
using System.Text;
using GatewayResource = CloudStreams.Core.Resources.Gateway;
using JsonSerializer = Neuroglia.Serialization.Json.JsonSerializer;

namespace CloudStreams.UnitTests.Cases.Transports;

/// <summary>
/// Covers the addresses of the transports other than HTTP, how cloud events are delivered over them, and how gateways ingest the messages they consume (see #218)
/// </summary>
public class TransportTests
{

    static CloudEvent Event(string id = "1") => new()
    {
        Id = id,
        SpecVersion = CloudEventSpecVersion.V1.Version,
        Source = new("https://orders.example.com"),
        Type = "order.placed",
        Subject = "order-1",
        DataContentType = "application/json",
        Data = new Dictionary<string, object>() { ["total"] = 42 }
    };

    [Theory]
    [InlineData("kafka://kafka:9092/orders", "kafka:9092", "orders")]
    [InlineData("kafka://kafka/orders?servers=kafka-2:9092,kafka-3:9092", "kafka:9092", "orders")]
    [InlineData("nats://nats/orders.placed?jetstream=true", "nats:4222", "orders.placed")]
    [InlineData("amqp://rabbitmq/?exchange=orders", "rabbitmq:5672", "")]
    [InlineData("amqps://rabbitmq/tenant-a?exchange=&routingKey=orders", "rabbitmq:5671", "tenant-a")]
    [InlineData("cloudstreams://gateway.example.com", "gateway.example.com:443", "")]
    [InlineData("cloudstreams://gateway:8080/cloud-streams?tls=false", "gateway:8080", "cloud-streams")]
    public void Valid_Sink_Addresses_Should_Be_Parsed(string uri, string server, string path)
    {
        // act
        var address = TransportAddress.Parse(new(uri));

        // assert
        Assert.Equal(server, address.Server);
        Assert.Equal(path, address.Path);
    }

    [Theory]
    [InlineData("kafka://user:secret@kafka/orders", "credentials")]
    [InlineData("kafka://kafka/", "Kafka topic")]
    [InlineData("kafka://kafka/orders?mechanism=gssapi", "mechanism")]
    [InlineData("nats://nats/orders.*", "wildcards")]
    [InlineData("nats://nats", "NATS subject")]
    [InlineData("amqp://rabbitmq/", "exchange")]
    [InlineData("amqp://rabbitmq/?exchange=", "routingKey")]
    [InlineData("kafka://kafka/orders?credentials=not%20valid", "credentials")]
    [InlineData("ftp://files/orders", "not supported")]
    public void Invalid_Sink_Addresses_Should_Be_Refused_With_A_Reason(string uri, string reason)
    {
        // act
        var valid = TransportAddress.TryParse(new(uri), false, out _, out var error);

        // assert
        Assert.False(valid);
        Assert.Contains(reason, error, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("nats://nats/orders.*", true)]
    [InlineData("nats://nats/orders.>", true)]
    [InlineData("amqp://rabbitmq/?queue=orders", true)]
    [InlineData("amqp://rabbitmq/?exchange=orders", false)]
    [InlineData("cloudstreams://gateway", false)]
    public void Channel_Addresses_Should_Allow_Wildcards_And_Require_A_Queue(string uri, bool valid)
    {
        Assert.Equal(valid, TransportAddress.TryParse(new(uri), true, out _, out _));
    }

    [Fact]
    public void The_Bridge_Should_Publish_To_The_Ingestion_Endpoint_Of_The_Other_Gateway()
    {
        Assert.Equal(new Uri("https://gateway.example.com:443/api/events/pub"), CloudStreamsBridge.GetEndpoint(TransportAddress.Parse(new("cloudstreams://gateway.example.com"))));
        Assert.Equal(new Uri("http://gateway:8080/cloud-streams/api/events/pub"), CloudStreamsBridge.GetEndpoint(TransportAddress.Parse(new("cloudstreams://gateway:8080/cloud-streams?tls=false"))));
    }

    [Theory]
    [InlineData("ce_type", "ce-type")]
    [InlineData("cloudEvents:source", "ce-source")]
    [InlineData("cloudEvents_specversion", "ce-specversion")]
    [InlineData("ce-id", "ce-id")]
    [InlineData("traceparent", "traceparent")]
    public void The_Attribute_Headers_Of_Every_Binding_Should_Be_Normalized_To_Those_Of_Http(string header, string expected)
    {
        Assert.Equal(expected, TransportMessageEncoding.NormalizeHeader(header));
    }

    [Fact]
    public void Credentials_That_Are_Not_Configured_Should_Be_Reported_With_How_To_Configure_Them()
    {
        // act
        var ex = Assert.Throws<InvalidOperationException>(() => new TransportOptions().GetCredentials(TransportAddress.Parse(new("kafka://kafka/orders?credentials=orders"))));

        // assert
        Assert.Contains("CLOUDSTREAMS__TRANSPORTS__CREDENTIALS__ORDERS__PASSWORD", ex.Message);
    }

    [Fact]
    public async Task Cloud_Events_Should_Be_Delivered_Through_The_Sink_Of_The_Scheme()
    {
        // arrange
        var sink = Substitute.For<ICloudEventSink>();
        sink.Schemes.Returns([TransportSchemes.Kafka]);
        var client = new SubscriberClient(new HttpClient(), [sink]);
        var events = new[] { Event("1"), Event("2") };

        // act
        var status = await client.SendBatchAsync(new("kafka://kafka/orders"), events, [new("cloudstreams-subscription", "orders")], "The server", default);

        // assert
        Assert.Equal(HttpStatusCode.OK, status);
        await sink.Received(1).SendAsync(Arg.Is<TransportAddress>(a => a.Path == "orders"), Arg.Is<IReadOnlyList<CloudEvent>>(e => e.Count == 2 && e[0].Id == "1"), Arg.Is<IReadOnlyList<KeyValuePair<string, string>>>(h => h.Single().Value == "orders"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_Transport_Failure_Should_Be_Reported_As_A_Retryable_Delivery_Failure()
    {
        // arrange
        var sink = Substitute.For<ICloudEventSink>();
        sink.Schemes.Returns([TransportSchemes.Nats]);
        sink.SendAsync(default!, default!, default!, default).ReturnsForAnyArgs(Task.FromException(new TimeoutException("no responders")));
        var client = new SubscriberClient(new HttpClient(), [sink]);

        // act
        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => client.SendAsync(new("nats://nats/orders"), Event(), [], "The server", default));

        // assert
        Assert.Null(ex.StatusCode);
        Assert.Contains("no responders", ex.Message);
        Assert.IsType<TimeoutException>(ex.InnerException);
    }

    [Fact]
    public async Task A_Transport_The_Broker_Does_Not_Support_Should_Be_Reported()
    {
        // arrange
        var client = new SubscriberClient(new HttpClient());

        // act
        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => client.SendAsync(new("amqp://rabbitmq/?exchange=orders"), Event(), [], "The server", default));

        // assert
        Assert.Contains("does not support the 'amqp' transport", ex.Message);
    }

    [Fact]
    public void Subscriptions_Should_Accept_Transport_Addresses_And_Refuse_Credentials_In_Them()
    {
        // arrange
        static Subscription Subscription(string uri) => new() { Metadata = new() { Name = "orders" }, Spec = new() { Subscriber = new() { Uri = new(uri) }, DeadLetter = new() { Uri = new("kafka://kafka/orders-dead-letters") } } };

        // act
        var valid = new SubscriptionValidator().Validate(Subscription("kafka://kafka/orders?credentials=orders"));
        var invalid = new SubscriptionValidator().Validate(Subscription("amqp://guest:guest@rabbitmq/?exchange=orders"));

        // assert
        Assert.True(valid.IsValid, string.Join("; ", valid.Errors));
        Assert.Contains(invalid.Errors, e => e.PropertyName == "spec.subscriber.uri" && e.ErrorMessage.Contains("credentials"));
    }

    [Fact]
    public void Gateway_Channels_Should_Be_Valid_And_Uniquely_Named()
    {
        // arrange
        static GatewayResource Gateway(params CloudEventIngestionChannel[] channels) => new(new ResourceMetadata("gateway"), new GatewaySpec() { Ingestion = new() { Channels = [.. channels] } });

        // act
        var valid = new GatewayValidator().Validate(Gateway(new() { Name = "orders", Uri = new("kafka://kafka/orders") }, new() { Name = "payments", Uri = new("nats://nats/payments.>?jetstream=true") }));
        var duplicated = new GatewayValidator().Validate(Gateway(new() { Name = "orders", Uri = new("kafka://kafka/orders") }, new() { Name = "orders", Uri = new("kafka://kafka/other") }));
        var unsupported = new GatewayValidator().Validate(Gateway(new CloudEventIngestionChannel() { Name = "orders", Uri = new("https://orders.example.com") }));

        // assert
        Assert.True(valid.IsValid, string.Join("; ", valid.Errors));
        Assert.Contains(duplicated.Errors, e => e.ErrorMessage.Contains("unique"));
        Assert.Contains(unsupported.Errors, e => e.PropertyName.StartsWith("spec.ingestion.channels"));
    }

    static (TestIngestionChannelService Service, IMediator Mediator) CreateIngestion()
    {
        var mediator = Substitute.For<IMediator>();
        var services = new ServiceCollection().AddSingleton(mediator).BuildServiceProvider();
        var service = new TestIngestionChannelService(services, Options.Create(new GatewayOptions() { Name = "gateway" }));
        return (service, mediator);
    }

    static readonly CloudEventIngestionChannel Channel = new() { Name = "orders", Uri = new("kafka://kafka/orders") };

    static TransportMessage Structured(params CloudEvent[] events) => events.Length == 1
        ? new(CloudEventContentType.Json, [], Encoding.UTF8.GetBytes(JsonSerializer.Default.SerializeToText(events[0])), "a message")
        : new(global::CloudStreams.Core.CloudEventExtensions.BatchContentType, [], Encoding.UTF8.GetBytes(JsonSerializer.Default.SerializeToText(events.ToList())), "a message");

    [Fact]
    public void Messages_Should_Be_Decoded_In_Every_Content_Mode()
    {
        // arrange
        var (ingestion, _) = CreateIngestion();
        var binary = new TransportMessage("application/json", [new("ce-specversion", "1.0"), new("ce-id", "3"), new("ce-source", "https://orders.example.com"), new("ce-type", "order.placed")], Encoding.UTF8.GetBytes("""{"total":42}"""), "a message");
        var untyped = Structured(Event("4")) with { ContentType = null };

        // act
        var structured = ingestion.Decode(Structured(Event("1")));
        var batch = ingestion.Decode(Structured(Event("1"), Event("2")));
        var decodedBinary = ingestion.Decode(binary);
        var decodedUntyped = ingestion.Decode(untyped);

        // assert
        Assert.Equal("1", Assert.Single(structured).Id);
        Assert.Equal(["1", "2"], batch.Select(e => e.Id));
        Assert.Equal("order.placed", Assert.Single(decodedBinary).Type);
        Assert.Equal("4", Assert.Single(decodedUntyped).Id);
    }

    [Fact]
    public async Task Consumed_Cloud_Events_Should_Be_Ingested_With_The_Channel_As_Publisher()
    {
        // arrange
        var (ingestion, mediator) = CreateIngestion();
        mediator.ExecuteAsync(Arg.Any<ConsumeEventCommand>(), Arg.Any<CancellationToken>()).Returns(new OperationResult((int)HttpStatusCode.Created));

        // act
        var acknowledged = await ingestion.HandleAsync(Channel, Structured(Event()), default);

        // assert
        Assert.True(acknowledged);
        await mediator.Received(1).ExecuteAsync(Arg.Is<ConsumeEventCommand>(c => c.Publisher == IngestionChannelService.PublisherPrefix + "orders" && c.CloudEvent.Id == "1"), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(HttpStatusCode.Created, true)]
    [InlineData(HttpStatusCode.Accepted, true)]
    [InlineData(HttpStatusCode.BadRequest, true)]
    [InlineData(HttpStatusCode.Forbidden, true)]
    [InlineData(HttpStatusCode.TooManyRequests, false)]
    [InlineData(HttpStatusCode.ServiceUnavailable, false)]
    public async Task Only_Cloud_Events_That_Could_Not_Be_Stored_For_Now_Should_Be_Consumed_Again(HttpStatusCode status, bool acknowledged)
    {
        // arrange
        var (ingestion, mediator) = CreateIngestion();
        mediator.ExecuteAsync(Arg.Any<ConsumeEventCommand>(), Arg.Any<CancellationToken>()).Returns(new OperationResult((int)status));

        // act
        var result = await ingestion.HandleAsync(Channel, Structured(Event()), default);

        // assert
        Assert.Equal(acknowledged, result);
    }

    [Fact]
    public async Task A_Message_That_Is_Not_A_Cloud_Event_Should_Be_Acknowledged_Without_Being_Ingested()
    {
        // arrange
        var (ingestion, mediator) = CreateIngestion();

        // act
        var acknowledged = await ingestion.HandleAsync(Channel, new("text/plain", [], Encoding.UTF8.GetBytes("hello"), "a message"), default);

        // assert
        Assert.True(acknowledged);
        await mediator.DidNotReceiveWithAnyArgs().ExecuteAsync(Arg.Any<ConsumeEventCommand>(), default);
    }

    sealed class TestIngestionChannelService(IServiceProvider serviceProvider, IOptions<GatewayOptions> options)
        : IngestionChannelService(NullLogger<IngestionChannelService>.Instance, serviceProvider, Substitute.For<ICloudEventAdmissionControl>(), [], JsonSerializer.Default, options)
    {

        public new IReadOnlyList<CloudEvent> Decode(TransportMessage message) => base.Decode(message);

        public new Task<bool> HandleAsync(CloudEventIngestionChannel channel, TransportMessage message, CancellationToken cancellationToken) => base.HandleAsync(channel, message, cancellationToken);

    }

}
