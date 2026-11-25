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
using CloudStreams.Dashboard.Services;
using Xunit;
using JsonSerializer = Neuroglia.Serialization.Json.JsonSerializer;
using YamlSerializer = Neuroglia.Serialization.Yaml.YamlSerializer;

namespace CloudStreams.Dashboard.UnitTests.Cases;

/// <summary>
/// Covers what the insight pages compute: the lag and acknowledgement rate of a subscription (#224), the tests of its playground (#224), and the health of the gateway (#225)
/// </summary>
public class InsightsTests
{

    static readonly DateTimeOffset Start = new(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Ack_Rates_Should_Be_Computed_Per_Minute_Between_Consecutive_Samples()
    {
        // arrange
        var history = new SubscriptionSampleHistory();
        history.Add(new(Start, 10, 100));
        history.Add(new(Start.AddSeconds(30), 5, 110));
        history.Add(new(Start.AddSeconds(60), 0, 110));

        // act
        var rates = history.GetAckRates();

        // assert
        Assert.Equal([20d, 0d], rates.Select(r => r.PerMinute));
        Assert.Equal(Start.AddSeconds(60), rates[^1].At);
    }

    [Fact]
    public void Ack_Rates_Should_Leave_Out_Replays_And_Unknown_Offsets()
    {
        // arrange
        var history = new SubscriptionSampleHistory();
        history.Add(new(Start, 0, 100));
        history.Add(new(Start.AddMinutes(1), 0, 0));
        history.Add(new(Start.AddMinutes(2), 0, null));
        history.Add(new(Start.AddMinutes(3), 0, 50));

        // act & assert
        Assert.Empty(history.GetAckRates());
    }

    [Fact]
    public void The_History_Should_Only_Keep_The_Most_Recent_Samples()
    {
        // arrange
        var history = new SubscriptionSampleHistory(3);

        // act
        for (var i = 0; i < 5; i++) history.Add(new(Start.AddSeconds(i), (ulong)i, null));

        // assert
        Assert.Equal([2ul, 3ul, 4ul], history.Samples.Select(s => s.Lag!.Value));
    }

    [Fact]
    public void A_Playground_Test_Should_Be_Built_From_Yaml_And_Json()
    {
        // arrange
        var filter = "type: attributes\nattributes:\n  type: order\\..*\n";
        var mutation = """{ "type": "expression", "expression": "${ . }" }""";
        var samples = """[{ "specversion": "1.0", "id": "1", "source": "https://orders.example.com", "type": "order.placed" }]""";

        // act
        var request = SubscriptionPlayground.BuildRequest(null, filter, mutation, samples, 20, JsonSerializer.Default, YamlSerializer.Default);

        // assert
        Assert.Equal(CloudEventFilterType.Attributes, request.Filter!.Type);
        Assert.Equal("order\\..*", request.Filter.Attributes!["type"]);
        Assert.Equal(CloudEventMutationType.Expression, request.Mutation!.Type);
        Assert.Equal("order.placed", Assert.Single(request.Events!).Type);
        Assert.Null(request.Length);
    }

    [Fact]
    public void A_Playground_Test_Without_Samples_Should_Read_Recent_Events()
    {
        // act
        var request = SubscriptionPlayground.BuildRequest(new(CloudEventPartitionType.BySource, "https://orders.example.com"), null, " ", null, 7, JsonSerializer.Default, YamlSerializer.Default);

        // assert
        Assert.Null(request.Filter);
        Assert.Null(request.Mutation);
        Assert.Null(request.Events);
        Assert.Equal(7, request.Length);
        Assert.Equal("https://orders.example.com", request.Partition!.Id);
    }

    [Theory]
    [InlineData("type: [", null, null)]
    [InlineData(null, "{ not json", null)]
    [InlineData(null, null, """[{ "specversion": "1.0", "id": "1" }]""")]
    [InlineData("type: expression\n---\ntype: attributes", null, null)]
    public void An_Invalid_Playground_Test_Should_Be_Refused(string? filter, string? mutation, string? samples)
    {
        Assert.Throws<FormatException>(() => SubscriptionPlayground.BuildRequest(null, filter, mutation, samples, 20, JsonSerializer.Default, YamlSerializer.Default));
    }

    [Fact]
    public void The_Filter_And_Mutation_Of_A_Subscription_Should_Round_Trip_Through_Yaml()
    {
        // arrange
        var filter = new CloudEventFilter() { Type = CloudEventFilterType.Expression, Expression = "${ .type == \"order.placed\" }" };

        // act
        var yaml = SubscriptionPlayground.ToYaml(filter, JsonSerializer.Default, YamlSerializer.Default);
        var request = SubscriptionPlayground.BuildRequest(null, yaml, null, null, 20, JsonSerializer.Default, YamlSerializer.Default);

        // assert
        Assert.Equal(filter.Expression, request.Filter!.Expression);
        Assert.Equal(string.Empty, SubscriptionPlayground.ToYaml(null, JsonSerializer.Default, YamlSerializer.Default));
    }

    [Fact]
    public void Detailed_Health_Should_List_Each_Check()
    {
        // arrange
        var json = """{ "status": "Degraded", "totalDuration": "00:00:00.01", "entries": { "redis": { "status": "Healthy", "description": "Redis answered in 1ms" }, "projections": { "status": "Degraded", "description": "The projections have not been set up yet" } } }""";

        // act
        var health = PlatformHealth.Parse(json);

        // assert
        Assert.Equal("degraded", health.Status);
        Assert.Equal([("redis", "healthy"), ("projections", "degraded")], health.Checks.Select(c => (c.Name, c.Status)));
        Assert.Equal("Redis answered in 1ms", health.Checks[0].Description);
    }

    [Theory]
    [InlineData("Healthy", "healthy")]
    [InlineData("{ not json", "unknown")]
    public void Undetailed_Health_Should_Only_Have_A_Status(string content, string status)
    {
        // act
        var health = PlatformHealth.Parse(content);

        // assert
        Assert.Equal(status, health.Status);
        Assert.Empty(health.Checks);
    }

}
