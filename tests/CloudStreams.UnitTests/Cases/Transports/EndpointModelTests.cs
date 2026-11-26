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

using CloudStreams.Core.Application.Validation;
using System.Text.Json.Nodes;
using GatewayResource = CloudStreams.Core.Resources.Gateway;
using JsonSerializer = Neuroglia.Serialization.Json.JsonSerializer;
using YamlSerializer = Neuroglia.Serialization.Yaml.YamlSerializer;

namespace CloudStreams.UnitTests.Cases.Transports;

/// <summary>
/// Covers how endpoints, authentication policies, secret references and feeds are written, in both their short and their long forms, and how they are validated (see #218)
/// </summary>
public class EndpointModelTests
{

    static Subscription Subscription(Action<SubscriptionSpec> configure)
    {
        var subscription = new Subscription() { Metadata = new() { Name = "orders" }, Spec = new() { Subscriber = new() } };
        configure(subscription.Spec);
        return subscription;
    }

    [Fact]
    public void An_Endpoint_Without_Authentication_Should_Be_Written_As_Its_Address()
    {
        // arrange
        var subscriber = new Subscriber() { Endpoint = new Uri("https://orders.example.com/events") };

        // act
        var json = JsonSerializer.Default.SerializeToNode(subscriber)!;
        var yaml = YamlSerializer.Default.Serialize(subscriber);

        // assert
        Assert.Equal("https://orders.example.com/events", json["endpoint"]!.GetValue<string>());
        Assert.Contains("endpoint: https://orders.example.com/events", yaml);
    }

    [Fact]
    public void An_Endpoint_With_Authentication_Should_Round_Trip_In_Json_And_Yaml()
    {
        // arrange
        var subscriber = new Subscriber()
        {
            Endpoint = new(new Uri("kafka://kafka:9092/orders"), new() { Basic = new() { Use = new("orders-kafka") } })
        };

        // act
        var fromJson = JsonSerializer.Default.Deserialize<Subscriber>(JsonSerializer.Default.SerializeToText(subscriber))!;
        var yaml = YamlSerializer.Default.Serialize(subscriber);
        var fromYaml = YamlSerializer.Default.Deserialize<Subscriber>(yaml)!;

        // assert
        foreach (var result in new[] { fromJson, fromYaml })
        {
            Assert.Equal(new Uri("kafka://kafka:9092/orders"), result.Endpoint!.Uri);
            Assert.Equal(AuthenticationScheme.Basic, result.Endpoint.Authentication!.Scheme);
            Assert.Equal("orders-kafka", result.Endpoint.Authentication.Basic!.Use!.Name);
            Assert.Null(result.Endpoint.Authentication.Basic.Use.Provider);
        }
        Assert.Contains("use: orders-kafka", yaml);
    }

    [Fact]
    public void A_Secret_Reference_Should_Name_Its_Provider_Explicitly_Or_Not()
    {
        // arrange
        const string json = """{ "basic": { "use": { "vault": "cloud-streams/orders" } }, "bearer": { "use": "orders-token" } }""";

        // act
        var policy = JsonSerializer.Default.Deserialize<AuthenticationPolicy>(json)!;
        var written = JsonSerializer.Default.SerializeToNode(policy)!;
        var fromYaml = YamlSerializer.Default.Deserialize<AuthenticationPolicy>(YamlSerializer.Default.Serialize(policy))!;

        // assert
        Assert.Equal(new SecretReference(SecretProviders.Vault, "cloud-streams/orders"), policy.Basic!.Use);
        Assert.Equal(new SecretReference("orders-token"), policy.Bearer!.Use);
        Assert.Equal("cloud-streams/orders", written["basic"]!["use"]!["vault"]!.GetValue<string>());
        Assert.Equal("orders-token", written["bearer"]!["use"]!.GetValue<string>());
        Assert.Equal(policy.Basic.Use, fromYaml.Basic!.Use);
    }

    [Fact]
    public void Feeds_Should_Be_Names_Or_Feeds_Of_The_Gateway_Own()
    {
        // arrange
        const string yaml = """
            feeds:
              - legacy-orders
              - name: payments
                endpoint: nats://nats:4222/payments.>?jetstream=true
            """;

        // act
        var ingestion = YamlSerializer.Default.Deserialize<CloudEventIngestionPolicy>(yaml)!;
        var json = JsonSerializer.Default.SerializeToNode(ingestion)!["feeds"]!.AsArray();

        // assert
        Assert.Equal("legacy-orders", ingestion.Feeds![0].Feed);
        Assert.Equal("payments", ingestion.Feeds[1].Name);
        Assert.Equal(new Uri("nats://nats:4222/payments.>?jetstream=true"), ingestion.Feeds[1].Endpoint!.Uri);
        Assert.Equal("legacy-orders", json[0]!.GetValue<string>());
        Assert.Equal("payments", json[1]!["name"]!.GetValue<string>());
        Assert.Equal(["legacy-orders", "payments"], ingestion.Feeds.Select(f => f.EffectiveName));
    }

    [Fact]
    public void A_Subscriber_Should_Deliver_To_Exactly_One_Of_An_Endpoint_And_A_Sink()
    {
        // act
        var endpoint = new SubscriptionValidator().Validate(Subscription(s => s.Subscriber.Endpoint = new Uri("https://orders.example.com")));
        var sink = new SubscriptionValidator().Validate(Subscription(s => s.Subscriber.Sink = "orders-kafka"));
        var neither = new SubscriptionValidator().Validate(Subscription(_ => { }));
        var both = new SubscriptionValidator().Validate(Subscription(s => { s.Subscriber.Endpoint = new Uri("https://orders.example.com"); s.Subscriber.Sink = "orders-kafka"; }));

        // assert
        Assert.True(endpoint.IsValid, string.Join("; ", endpoint.Errors));
        Assert.True(sink.IsValid, string.Join("; ", sink.Errors));
        Assert.Contains(neither.Errors, e => e.PropertyName == "spec.subscriber");
        Assert.Contains(both.Errors, e => e.PropertyName == "spec.subscriber");
    }

    public static TheoryData<string, AuthenticationPolicy, string> InvalidAuthentications => new()
    {
        { "kafka://kafka/orders", new() { ApiKey = new() { Key = "key" } }, "does not support the 'apiKey' authentication" },
        { "nats://nats/orders", new() { OAuth2 = new() { Authority = new("https://login.example.com"), Client = new() { Id = "id", Secret = "secret" } } }, "does not support" },
        { "https://orders.example.com", new() { Basic = new() { Username = "user" } }, "password" },
        { "https://orders.example.com", new() { Basic = new(), Bearer = new() }, "exactly one" },
        { "https://orders.example.com", new() { OAuth2 = new() { Client = new() { Id = "id", Secret = "secret" } } }, "authority" },
        { "https://orders.example.com", new() { Oidc = new() { Authority = new("https://login.example.com"), Client = new() { Id = "id" } } }, "client.secret" },
        { "https://orders.example.com", new() { Basic = new() { Use = new("unknown", "orders") } }, "is not a secret provider" }
    };

    [Theory]
    [MemberData(nameof(InvalidAuthentications))]
    public void Invalid_Authentications_Should_Be_Refused_With_A_Reason(string uri, AuthenticationPolicy authentication, string reason)
    {
        // act
        var result = new SubscriptionValidator().Validate(Subscription(s => s.Subscriber.Endpoint = new Endpoint(new Uri(uri), authentication)));

        // assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName.StartsWith("spec.subscriber.endpoint.authentication") && e.ErrorMessage.Contains(reason));
    }

    [Fact]
    public void A_Policy_That_Uses_A_Secret_Should_Not_Require_Its_Values()
    {
        // act
        var result = new SubscriptionValidator().Validate(Subscription(s => s.Subscriber.Endpoint = new Endpoint(new Uri("https://orders.example.com"), new() { OAuth2 = new() { Use = new("orders-oauth2"), Scopes = ["events"] } })));

        // assert
        Assert.True(result.IsValid, string.Join("; ", result.Errors));
    }

    [Fact]
    public void Gateway_Feeds_Should_Be_Valid_And_Uniquely_Named()
    {
        // arrange
        static GatewayResource Gateway(params FeedReference[] feeds) => new(new ResourceMetadata("gateway"), new GatewaySpec() { Ingestion = new() { Feeds = [.. feeds] } });

        // act
        var valid = new GatewayValidator().Validate(Gateway("legacy-orders", new FeedReference() { Name = "payments", Endpoint = new Uri("nats://nats/payments.>?jetstream=true") }));
        var duplicated = new GatewayValidator().Validate(Gateway("orders", new FeedReference() { Name = "orders", Endpoint = new Uri("kafka://kafka/orders") }));
        var unconsumable = new GatewayValidator().Validate(Gateway(new FeedReference() { Name = "orders", Endpoint = new Uri("https://orders.example.com") }));

        // assert
        Assert.True(valid.IsValid, string.Join("; ", valid.Errors));
        Assert.Contains(duplicated.Errors, e => e.ErrorMessage.Contains("more than once"));
        Assert.Contains(unconsumable.Errors, e => e.PropertyName == "spec.ingestion.feeds[0].endpoint");
    }

    [Fact]
    public void Sinks_And_Feeds_Should_Be_Validated_As_Delivery_And_Consumption_Endpoints()
    {
        // act
        var sink = new SinkValidator().Validate(new Sink(new("orders"), new() { Endpoint = new Uri("amqp://rabbitmq/?exchange=orders") }));
        var feedOfAnExchange = new FeedValidator().Validate(new Feed(new("orders"), new() { Endpoint = new Uri("amqp://rabbitmq/?exchange=orders") }));
        var feed = new FeedValidator().Validate(new Feed(new("orders"), new() { Endpoint = new Uri("amqp://rabbitmq/?queue=orders") }));

        // assert
        Assert.True(sink.IsValid, string.Join("; ", sink.Errors));
        Assert.Contains(feedOfAnExchange.Errors, e => e.ErrorMessage.Contains("queue"));
        Assert.True(feed.IsValid, string.Join("; ", feed.Errors));
    }

    [Fact]
    public void An_Address_With_Credentials_Should_Be_Refused()
    {
        // act
        var result = new SubscriptionValidator().Validate(Subscription(s => s.Subscriber.Endpoint = new Uri("amqp://guest:guest@rabbitmq/?exchange=orders")));

        // assert
        Assert.Contains(result.Errors, e => e.PropertyName == "spec.subscriber.endpoint" && e.ErrorMessage.Contains("credentials"));
    }

}
