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
using Microsoft.Extensions.DependencyInjection;
using Neuroglia.Data.Expressions.JQ;
using Neuroglia.Data.Expressions.Services;
using Neuroglia.Serialization;
using System.Text.Json.Nodes;

namespace CloudStreams.UnitTests.Cases.Core;

/// <summary>
/// Covers the mapping of the CloudEvents Subscriptions API onto Cloud Streams subscriptions (see #219)
/// </summary>
public class CloudEventSubscriptionsTests
{

    static readonly SubscriptionEventPipeline Pipeline = new(
        new ServiceCollection().AddLogging().AddSingleton<ISerializerProvider, SerializerProvider>().AddSingleton<ISerializer>(Neuroglia.Serialization.Json.JsonSerializer.Default).AddJQExpressionEvaluator().BuildServiceProvider().GetRequiredService<IExpressionEvaluator>(),
        [], new HttpClient(), Neuroglia.Serialization.Json.JsonSerializer.Default);

    static JsonObject Filter(string json) => JsonNode.Parse(json)!.AsObject();

    static CloudEventRecord Record(string type, string source = "https://github.com/cloudevents", string? subject = null)
    {
        var attributes = new Dictionary<string, object>() { ["specversion"] = "1.0", ["id"] = Guid.NewGuid().ToString(), ["type"] = type, ["source"] = source };
        if (subject != null) attributes["subject"] = subject;
        return new("cloud-events", 0, new(attributes), null);
    }

    static async Task<bool> SelectsAsync(CloudEventSubscription subscription, CloudEventRecord record)
    {
        var resource = new Subscription() { Metadata = new() { Name = "test" }, Spec = new() { Subscriber = new() { Uri = new("http://localhost") }, Filter = CloudEventSubscriptionFilter.Compile(subscription) } };
        return await Pipeline.FiltersAsync(resource, record);
    }

    [Fact]
    public void A_Subscription_Without_Constraints_Should_Select_Every_Event()
    {
        Assert.Null(CloudEventSubscriptionFilter.Compile(new() { Filters = [] }));
    }

    [Fact]
    public async Task Exact_Prefix_And_Suffix_Filters_On_Different_Attributes_Should_Compile_Into_Attribute_Filters()
    {
        // arrange
        var subscription = new CloudEventSubscription()
        {
            Source = "https://github.com/cloudevents",
            Filters = [Filter("""{ "prefix": { "type": "com.github." } }"""), Filter("""{ "suffix": { "subject": "/spec" } }""")]
        };

        // act
        var filter = CloudEventSubscriptionFilter.Compile(subscription);

        // assert
        Assert.Equal(CloudEventFilterType.Attributes, filter!.Type);
        Assert.Equal(3, filter.Attributes!.Count);
        Assert.True(await SelectsAsync(subscription, Record("com.github.push", subject: "/cloudevents/spec")));
        Assert.False(await SelectsAsync(subscription, Record("com.gitlab.push", subject: "/cloudevents/spec")));
        Assert.False(await SelectsAsync(subscription, Record("com.github.push", subject: "/cloudevents/sdk")));
        Assert.False(await SelectsAsync(subscription, Record("com.github.push", "https://github.com/other", "/cloudevents/spec")));
        // a prefix is matched literally: the dot is not a wildcard
        Assert.False(await SelectsAsync(subscription, Record("comXgithub.push", subject: "/cloudevents/spec")));
    }

    [Fact]
    public async Task Types_Should_Select_Events_Of_Any_Of_Them()
    {
        var subscription = new CloudEventSubscription() { Types = ["com.github.push", "com.github.pull_request"] };
        Assert.Equal(CloudEventFilterType.Attributes, CloudEventSubscriptionFilter.Compile(subscription)!.Type);
        Assert.True(await SelectsAsync(subscription, Record("com.github.push")));
        Assert.True(await SelectsAsync(subscription, Record("com.github.pull_request")));
        Assert.False(await SelectsAsync(subscription, Record("com.github.push.v2")));
    }

    [Fact]
    public async Task Any_And_Not_Filters_Should_Compile_Into_An_Expression_That_Applies_Them()
    {
        // arrange - push or pull request events, but not those of the 'spec' repository
        var subscription = new CloudEventSubscription()
        {
            Filters =
            [
                Filter("""{ "any": [ { "exact": { "type": "com.github.push" } }, { "exact": { "type": "com.github.pull_request" } } ] }"""),
                Filter("""{ "not": { "suffix": { "subject": "/spec" } } }""")
            ]
        };

        // act
        var filter = CloudEventSubscriptionFilter.Compile(subscription);

        // assert
        Assert.Equal(CloudEventFilterType.Expression, filter!.Type);
        Assert.True(await SelectsAsync(subscription, Record("com.github.push", subject: "/cloudevents/sdk")));
        Assert.True(await SelectsAsync(subscription, Record("com.github.pull_request", subject: "/cloudevents/sdk")));
        Assert.False(await SelectsAsync(subscription, Record("com.github.push", subject: "/cloudevents/spec")));
        Assert.False(await SelectsAsync(subscription, Record("com.github.issue", subject: "/cloudevents/sdk")));
        // an event without a subject does not end with '/spec': it is selected
        Assert.True(await SelectsAsync(subscription, Record("com.github.push")));
    }

    [Fact]
    public async Task Values_Should_Be_Escaped_In_Expressions()
    {
        var subscription = new CloudEventSubscription() { Filters = [Filter("""{ "not": { "exact": { "subject": "a \"quoted\" ${ subject }" } } }""")] };
        Assert.False(await SelectsAsync(subscription, Record("t", subject: "a \"quoted\" ${ subject }")));
        Assert.True(await SelectsAsync(subscription, Record("t", subject: "something else")));
    }

    [Theory]
    [InlineData("""{ "sql": "source LIKE '%cloudevents%'" }""", "'sql' filter dialect")]
    [InlineData("""{ "regex": { "type": ".*" } }""", "'regex' filter dialect")]
    [InlineData("""{ "exact": { "type": "a" }, "prefix": { "type": "b" } }""", "single property")]
    [InlineData("""{ "exact": { } }""", "at least one attribute")]
    [InlineData("""{ "exact": { "type": "" } }""", "non-empty string")]
    [InlineData("""{ "all": [ ] }""", "at least one filter expression")]
    public void Invalid_Or_Unsupported_Filters_Should_Be_Refused(string filter, string reason)
    {
        var exception = Assert.Throws<ProblemDetailsException>(() => CloudEventSubscriptionFilter.Compile(new() { Filters = [Filter(filter)] }));
        Assert.Equal(400, exception.Problem.Status);
        Assert.Contains(reason, exception.Problem.Detail);
    }

    [Fact]
    public void Subscriptions_Created_Otherwise_Should_Be_Described_As_Well_As_They_Can_Be()
    {
        // arrange
        var resource = new Subscription()
        {
            Metadata = new() { Name = "orders" },
            Spec = new()
            {
                Partition = new(CloudEventPartitionType.BySource, "https://orders.example.com"),
                Subscriber = new() { Uri = new("https://subscriber.example.com/events") },
                Filter = new() { Type = CloudEventFilterType.Attributes, Attributes = new Dictionary<string, string?>() { ["type"] = "^order\\.placed\\z", ["subject"] = "^eu/", ["region"] = "(eu|us)" } },
                Stream = new() { Offset = 0 }
            }
        };

        // act
        var described = new CloudEventSubscriptionManager(Substitute.For<IResourceRepository>(), Neuroglia.Serialization.Json.JsonSerializer.Default).Describe(resource);

        // assert - a pattern that is not an anchored literal cannot be described, and is left out
        Assert.Equal("orders", described.Id);
        Assert.Equal("https://subscriber.example.com/events", described.Sink);
        Assert.Equal("HTTP", described.Protocol);
        Assert.Equal("https://orders.example.com", described.Source);
        Assert.Equal(2, described.Filters!.Count);
        Assert.Equal("order.placed", described.Filters.Single(f => f.ContainsKey("exact"))["exact"]!["type"]!.GetValue<string>());
        Assert.Equal("eu/", described.Filters.Single(f => f.ContainsKey("prefix"))["prefix"]!["subject"]!.GetValue<string>());
        Assert.Equal(0, described.Config![CloudEventSubscriptionManager.OffsetConfigKey]!.GetValue<long>());
    }

    static readonly CloudEventSubscription Valid = new() { Sink = "https://subscriber.example.com/events", Protocol = "HTTP" };

    public static TheoryData<CloudEventSubscription, string> InvalidSubscriptions => new()
    {
        { Valid with { Protocol = null }, "'protocol' is required" },
        { Valid with { Protocol = "MQTT5" }, "only delivers cloud events with 'HTTP'" },
        { Valid with { Protocol = "http" }, "only delivers cloud events with 'HTTP'" },
        { Valid with { Sink = "ftp://subscriber.example.com" }, "'sink' is required" },
        { Valid with { SinkCredential = new() { ["credentialtype"] = "PLAIN" } }, "'sinkcredential' is not supported" },
        { Valid with { ProtocolSettings = new() { ["method"] = "PUT" } }, "'POST' method" },
        { Valid with { ProtocolSettings = new() { ["headers"] = new JsonObject() } }, "'headers' is not supported" },
        { Valid with { Config = new() { ["retries"] = 3 } }, "'retries' is not supported" },
        { Valid with { Config = new() { ["offset"] = "start" } }, "must be an integer" }
    };

    [Theory]
    [MemberData(nameof(InvalidSubscriptions))]
    public async Task Invalid_Or_Unsupported_Subscriptions_Should_Be_Refused(CloudEventSubscription subscription, string reason)
    {
        var repository = Substitute.For<IResourceRepository>();
        var manager = new CloudEventSubscriptionManager(repository, Neuroglia.Serialization.Json.JsonSerializer.Default);
        var exception = await Assert.ThrowsAsync<ProblemDetailsException>(() => manager.CreateAsync(subscription));
        Assert.Equal(400, exception.Problem.Status);
        Assert.Contains(reason, exception.Problem.Detail);
        Assert.Empty(repository.ReceivedCalls());
    }

}
