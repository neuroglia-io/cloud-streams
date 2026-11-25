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
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol.Transport;
using Neuroglia.Data.Infrastructure.ResourceOriented;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace CloudStreams.IntegrationTests.Cases;

/// <summary>
/// Calls the gateway's MCP tools, end to end (#183, #217)
/// </summary>
[Collection(CloudStreamsCollection.Name)]
public sealed class McpTests(CloudStreamsFixture fixture, ITestOutputHelper output)
    : IAsyncLifetime
{

    static Uri Source([CallerMemberName] string test = "") => new($"https://e2e.cloud-streams.io/{test.ToLowerInvariant()}");

    static string Name([CallerMemberName] string test = "") => test.ToLowerInvariant().Replace('_', '-');

    IReadOnlyList<int>? _logs;

    public Task InitializeAsync()
    {
        this._logs = fixture.CheckpointLogs();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        output.WriteLine(fixture.GetLogs(this._logs));
        return Task.CompletedTask;
    }

    Task<IMcpClient> ConnectAsync() => McpClientFactory.CreateAsync(new SseClientTransport(new SseClientTransportOptions() { Endpoint = new(fixture.GatewayUri, "mcp/sse"), Name = "e2e" }));

    static string Text(ModelContextProtocol.Protocol.Types.CallToolResponse response) => string.Concat(response.Content.Select(c => c.Text));

    [Fact]
    public async Task Agents_Should_Discover_The_Tools()
    {
        // arrange
        await using var client = await ConnectAsync();

        // act
        var tools = await client.ListToolsAsync();

        // assert
        Assert.Superset(new HashSet<string>() { "ListPartitions", "ListSubscriptions", "GetSubscription", "ListDeadLetters", "SuspendSubscription", "ResumeSubscription", "ReplaySubscription", "PublishEvent" }, tools.Select(t => t.Name).ToHashSet());
    }

    [Fact]
    public async Task Agents_Should_Publish_Events_And_Operate_Subscriptions()
    {
        // arrange
        var endpoint = fixture.Subscriber.Register(Name());
        await fixture.Api.Resources.Subscriptions.CreateAsync(new Subscription()
        {
            Metadata = new() { Name = Name() },
            Spec = new() { Partition = new(CloudEventPartitionType.BySource, Source().OriginalString), Subscriber = new() { Uri = endpoint.Uri }, Stream = new() { Offset = 0 } }
        });
        await using var client = await ConnectAsync();
        var cloudEvent = new Dictionary<string, object?>() { ["specversion"] = "1.0", ["id"] = "mcp-0", ["source"] = Source().OriginalString, ["type"] = "io.cloud-streams.e2e.v1", ["data"] = new { by = "an agent" } };

        // act - an agent publishes an event, then suspends and lists the subscription
        var published = await client.CallToolAsync("PublishEvent", new Dictionary<string, object?>() { ["cloudEvent"] = cloudEvent });
        var delivery = (await endpoint.WaitForAsync(1))[0];
        var suspended = await client.CallToolAsync("SuspendSubscription", new Dictionary<string, object?>() { ["name"] = Name() });
        var listed = await client.CallToolAsync("ListSubscriptions", new Dictionary<string, object?>());
        var partitions = await client.CallToolAsync("ListPartitions", new Dictionary<string, object?>() { ["type"] = "by-source", ["prefix"] = Source().OriginalString });

        // assert
        Assert.Contains("stored", Text(published));
        Assert.Equal("mcp-0", delivery.Id);
        Assert.Equal(true, JsonDocument.Parse(Text(suspended)).RootElement.GetProperty("spec").GetProperty("suspended").GetBoolean());
        Assert.Contains(JsonDocument.Parse(Text(listed)).RootElement.EnumerateArray(), s => s.GetProperty("name").GetString() == Name());
        Assert.Contains(Source().OriginalString, Text(partitions));
    }

}
