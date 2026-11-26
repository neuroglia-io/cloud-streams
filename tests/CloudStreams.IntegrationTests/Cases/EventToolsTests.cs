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
using Microsoft.AspNetCore.SignalR.Client;
using Neuroglia;
using Neuroglia.Data.Infrastructure.ResourceOriented;
using Neuroglia.Eventing.CloudEvents;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace CloudStreams.IntegrationTests.Cases;

/// <summary>
/// Verifies, end to end, the tools the dashboard uses to watch, search, publish and redeliver cloud events (#221, #222, #223)
/// </summary>
[Collection(CloudStreamsCollection.Name)]
public sealed class EventToolsTests(CloudStreamsFixture fixture, ITestOutputHelper output)
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

    static CloudEvent Event(Uri source, string id, string type, int total) => new() { SpecVersion = "1.0", Id = id, Source = source, Type = type, DataContentType = "application/json", Data = new { total } };

    [Fact]
    public async Task The_Live_Tail_Should_Only_Stream_The_Events_Of_The_Subscribed_Source_And_Type()
    {
        // arrange
        var received = new ConcurrentQueue<string>();
        await using var connection = new HubConnectionBuilder().WithUrl(new Uri(fixture.GatewayUri, "api/ws/events")).Build();
        connection.On<JsonElement>("StreamEvent", e => received.Enqueue(e.GetProperty("id").GetString()!));
        await connection.StartAsync();
        await connection.InvokeAsync("Subscribe", Source().OriginalString, "com.example.order.placed");

        // act
        await fixture.Api.CloudEvents.PublishAsync(Event(Source(), "order", "com.example.order.placed", 1));
        await fixture.Api.CloudEvents.PublishAsync(Event(Source(), "invoice", "com.example.invoice.issued", 2));
        await fixture.Api.CloudEvents.PublishAsync(Event(new("https://e2e.cloud-streams.io/elsewhere"), "elsewhere", "com.example.order.placed", 3));
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (received.IsEmpty && DateTime.UtcNow < deadline) await Task.Delay(100);
        await Task.Delay(1000);

        // assert
        Assert.Equal(["order"], received);
    }

    [Fact]
    public async Task Events_Should_Be_Published_Searched_And_Redelivered_From_The_Management_Api()
    {
        // arrange
        var endpoint = fixture.Subscriber.Register(Name());
        await fixture.Api.Resources.Subscriptions.CreateAsync(new Subscription()
        {
            Metadata = new() { Name = Name() },
            Spec = new() { Partition = new(CloudEventPartitionType.BySource, Source().OriginalString), Subscriber = new() { Endpoint = endpoint.Uri }, Stream = new() { Offset = 0 } }
        });

        // act - publish
        for (var i = 0; i < 5; i++)
        {
            var publication = await fixture.Api.CloudEvents.PublishAsync(Event(Source(), $"event-{i}", i % 2 == 0 ? "com.example.order.placed" : "com.example.invoice.issued", i * 10));
            Assert.Equal(CloudStreamsHeaders.Outcomes.Stored, publication.Outcome);
        }
        await endpoint.WaitForAsync(5);

        // act - search the partition by attribute and data
        var found = await fixture.Api.CloudEvents.SearchAsync(new()
        {
            PartitionType = CloudEventPartitionType.BySource,
            PartitionId = Source().OriginalString,
            Attributes = new() { ["type"] = "order" },
            Expression = "${ .data.total >= 20 }"
        });

        // act - redeliver the last order, by its offset in the partition
        var last = found.Records[^1];
        var redelivery = await fixture.Api.Resources.Subscriptions.RedeliverAsync(Name(), new() { Partition = new(CloudEventPartitionType.BySource, Source().OriginalString), Offset = last.Sequence });
        var missing = await Assert.ThrowsAsync<ProblemDetailsException>(() => fixture.Api.Resources.Subscriptions.RedeliverAsync(Name(), new() { Partition = new(CloudEventPartitionType.BySource, Source().OriginalString), Offset = 999 }));
        var deliveries = await endpoint.WaitForAsync(6);

        // assert
        Assert.Equal(["event-2", "event-4"], found.Records.Select(r => r.Metadata.ContextAttributes[CloudEventAttributes.Id].ToString()));
        Assert.All(found.Records, r => Assert.NotNull(r.Timestamp));
        Assert.Equal("event-4", redelivery.Id);
        Assert.Equal(200, redelivery.Status);
        Assert.Equal("event-4", deliveries[^1].Id);
        Assert.Equal((int)System.Net.HttpStatusCode.NotFound, missing.Problem.Status);
    }

}
