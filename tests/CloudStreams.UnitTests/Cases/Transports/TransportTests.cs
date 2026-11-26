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
    public void Feed_Addresses_Should_Allow_Wildcards_And_Require_A_Queue(string uri, bool valid)
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

    sealed class RecordingHandler
        : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            this.Requests.Add(request);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
        }
    }

    [Fact]
    public async Task Cloud_Events_Should_Be_Delivered_Through_The_Sink_Of_The_Scheme()
    {
        // arrange
        var sink = Substitute.For<ICloudEventSink>();
        sink.Schemes.Returns([TransportSchemes.Kafka]);
        var client = new CloudEventDeliveryClient(new HttpClient(), [sink]);
        var events = new[] { Event("1"), Event("2") };

        // act
        var status = await client.SendBatchAsync(new(new Uri("kafka://kafka/orders"), null), events, [new("cloudstreams-subscription", "orders")], "The server", default);

        // assert
        Assert.Equal(HttpStatusCode.OK, status);
        await sink.Received(1).SendAsync(Arg.Is<TransportAddress>(a => a!.Path == "orders"), null, Arg.Is<IReadOnlyList<CloudEvent>>(e => e!.Count == 2 && e[0].Id == "1"), Arg.Is<IReadOnlyList<KeyValuePair<string, string>>>(h => h!.Single().Value == "orders"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Cloud_Events_Should_Be_Delivered_To_The_Endpoint_Of_A_Sink_With_Its_Credentials()
    {
        // arrange
        var handler = new RecordingHandler();
        var endpoints = Substitute.For<IEndpointResolver>();
        endpoints.GetSinkEndpointAsync("orders-webhook", Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(new Endpoint(new Uri("https://orders.example.com/events"), new() { Bearer = new() { Use = new("orders-token") } }));
        var credentials = Substitute.For<ICredentialResolver>();
        credentials.ResolveAsync(Arg.Any<AuthenticationPolicy>(), Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(TransportCredentials.Bearer("token"));
        var client = new CloudEventDeliveryClient(new HttpClient(handler), [], credentials, endpoints);

        // act
        var status = await client.SendAsync(new(null, "orders-webhook"), Event(), [], "The server", default);

        // assert
        Assert.Equal(HttpStatusCode.NoContent, status);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(new Uri("https://orders.example.com/events"), request.RequestUri);
        Assert.Equal("Bearer token", request.Headers.Authorization!.ToString());
    }

    [Fact]
    public async Task A_Missing_Sink_Should_Be_Reported_As_A_Retryable_Delivery_Failure()
    {
        // arrange
        var endpoints = Substitute.For<IEndpointResolver>();
        endpoints.GetSinkEndpointAsync("deleted", Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(Task.FromException<Endpoint>(new InvalidOperationException("The sink 'deleted' does not exist")));
        var client = new CloudEventDeliveryClient(new HttpClient(), [], null, endpoints);

        // act
        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => client.SendAsync(new(null, "deleted"), Event(), [], "The server", default));

        // assert
        Assert.Null(ex.StatusCode);
        Assert.Contains("does not exist", ex.Message);
    }

    [Fact]
    public async Task A_Transport_Failure_Should_Be_Reported_As_A_Retryable_Delivery_Failure()
    {
        // arrange
        var sink = Substitute.For<ICloudEventSink>();
        sink.Schemes.Returns([TransportSchemes.Nats]);
        sink.SendAsync(default!, default, default!, default!, default).ReturnsForAnyArgs(Task.FromException(new TimeoutException("no responders")));
        var client = new CloudEventDeliveryClient(new HttpClient(), [sink]);

        // act
        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => client.SendAsync(new(new Uri("nats://nats/orders"), null), Event(), [], "The server", default));

        // assert
        Assert.Null(ex.StatusCode);
        Assert.Contains("no responders", ex.Message);
        Assert.IsType<TimeoutException>(ex.InnerException);
    }

    [Fact]
    public async Task A_Transport_That_Is_Not_Supported_Should_Be_Reported()
    {
        // arrange
        var client = new CloudEventDeliveryClient(new HttpClient());

        // act
        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => client.SendAsync(new(new Uri("amqp://rabbitmq/?exchange=orders"), null), Event(), [], "The server", default));

        // assert
        Assert.Contains("'amqp' transport", ex.Message);
    }


    static (TestIngestionFeedService Service, IMediator Mediator) CreateIngestion()
    {
        var mediator = Substitute.For<IMediator>();
        var services = new ServiceCollection().AddSingleton(mediator).BuildServiceProvider();
        var service = new TestIngestionFeedService(services, Gateway);
        return (service, mediator);
    }

    const string Feed = "orders";

    static readonly global::CloudStreams.Core.Resources.Gateway Gateway = new(new("gateway", Neuroglia.Data.Infrastructure.ResourceOriented.Namespace.DefaultNamespaceName), new());

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
    public async Task Consumed_Cloud_Events_Should_Be_Ingested_With_The_Feed_As_Publisher()
    {
        // arrange
        var (ingestion, mediator) = CreateIngestion();
        mediator.ExecuteAsync(Arg.Any<ConsumeEventCommand>(), Arg.Any<CancellationToken>()).Returns(new OperationResult((int)HttpStatusCode.Created));

        // act
        var acknowledged = await ingestion.HandleAsync(Feed, Structured(Event()), default);

        // assert
        Assert.True(acknowledged);
        await mediator.Received(1).ExecuteAsync(Arg.Is<ConsumeEventCommand>(c => c!.Publisher == IngestionFeedService.PublisherPrefix + "orders" && c.CloudEvent.Id == "1"), Arg.Any<CancellationToken>());
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
        var result = await ingestion.HandleAsync(Feed, Structured(Event()), default);

        // assert
        Assert.Equal(acknowledged, result);
    }

    [Fact]
    public async Task A_Message_That_Is_Not_A_Cloud_Event_Should_Be_Acknowledged_Without_Being_Ingested()
    {
        // arrange
        var (ingestion, mediator) = CreateIngestion();

        // act
        var acknowledged = await ingestion.HandleAsync(Feed, new("text/plain", [], Encoding.UTF8.GetBytes("hello"), "a message"), default);

        // assert
        Assert.True(acknowledged);
        await mediator.DidNotReceiveWithAnyArgs().ExecuteAsync(Arg.Any<ConsumeEventCommand>(), default);
    }

    sealed class TestIngestionFeedService(IServiceProvider serviceProvider, global::CloudStreams.Core.Resources.Gateway gateway)
        : IngestionFeedService(NullLogger<IngestionFeedService>.Instance, serviceProvider, Configuration(gateway), [], JsonSerializer.Default)
    {

        public new IReadOnlyList<CloudEvent> Decode(TransportMessage message) => base.Decode(message);

        public Task<bool> HandleAsync(string feed, TransportMessage message, CancellationToken cancellationToken) => base.HandleAsync(new FeedTarget(gateway.GetNamespace()!, gateway.GetName(), feed), message, cancellationToken);

        static IGatewayConfiguration Configuration(global::CloudStreams.Core.Resources.Gateway gateway)
        {
            var configuration = Substitute.For<IGatewayConfiguration>();
            configuration.Gateway.Returns(gateway);
            configuration.IsReady.Returns(true);
            return configuration;
        }

    }

}
