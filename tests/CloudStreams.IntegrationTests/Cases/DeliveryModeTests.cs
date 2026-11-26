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
using Neuroglia;
using Neuroglia.Data;
using Neuroglia.Data.Infrastructure.ResourceOriented;
using Neuroglia.Eventing.CloudEvents;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace CloudStreams.IntegrationTests.Cases;

/// <summary>
/// Verifies, end to end, the delivery of cloud events in batches, and in parallel in order per key (#212)
/// </summary>
[Collection(CloudStreamsCollection.Name)]
public sealed class DeliveryModeTests(CloudStreamsFixture fixture, ITestOutputHelper output)
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

    async Task PublishAsync(Uri source, IEnumerable<(string Id, string? Subject)> events)
    {
        foreach (var (id, subject) in events)
        {
            var json = $"{{\"specversion\":\"1.0\",\"id\":\"{id}\",\"source\":\"{source}\",\"type\":\"io.cloud-streams.e2e.v1\"{(subject == null ? "" : $",\"subject\":\"{subject}\"")},\"data\":{{\"id\":\"{id}\"}}}}";
            using var response = await fixture.Gateway.PostAsync("api/events/pub", new StringContent(json, Encoding.UTF8, CloudEventContentType.Json));
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }
    }

    async Task<Subscription> SubscribeAsync(string name, Uri source, TestSubscriber.Endpoint endpoint, Action<SubscriptionSpec>? configure = null)
    {
        var spec = new SubscriptionSpec() { Partition = new(CloudEventPartitionType.BySource, source.OriginalString), Subscriber = new() { Endpoint = endpoint.Uri }, Stream = new() { Offset = 0 } };
        configure?.Invoke(spec);
        return await fixture.Api.Resources.Subscriptions.CreateAsync(new Subscription() { Metadata = new() { Name = name }, Spec = spec });
    }

    async Task WaitForAckedOffsetAsync(string name, ulong offset)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (true)
        {
            var subscription = await fixture.Api.Resources.Subscriptions.GetAsync(name);
            if (subscription.Status?.Stream?.AckedOffset == offset) return;
            if (DateTime.UtcNow > deadline) throw new TimeoutException($"The acknowledged offset of subscription '{name}' did not reach {offset}: {Neuroglia.Serialization.Json.JsonSerializer.Default.SerializeToText(subscription.Status)}");
            await Task.Delay(250);
        }
    }

    [Fact]
    public async Task Events_Should_Be_Delivered_In_Batches()
    {
        // arrange
        var endpoint = fixture.Subscriber.Register(Name());
        await SubscribeAsync(Name(), Source(), endpoint, spec => spec.Subscriber.Batch = new() { MaxSize = 3 });
        string[] ids = [.. Enumerable.Range(0, 7).Select(i => $"event-{i}")];

        // act
        await PublishAsync(Source(), ids.Select(id => (id, (string?)null)));

        // assert
        var deliveries = await endpoint.WaitForAsync(7);
        Assert.Equal(ids, deliveries.Select(d => d.Id));
        Assert.All(deliveries, d => Assert.True(d.Batched));
        await WaitForAckedOffsetAsync(Name(), 7);
    }

    [Fact]
    public async Task Events_Should_Be_Delivered_In_Order_Per_Key()
    {
        // arrange
        var endpoint = fixture.Subscriber.Register(Name());
        await SubscribeAsync(Name(), Source(), endpoint, spec => spec.Subscriber.Ordering = new() { Key = CloudEventAttributes.Subject, MaxConcurrency = 4 });
        (string Id, string? Subject)[] events = [.. Enumerable.Range(0, 12).Select(i => ($"event-{i}", (string?)$"order-{i % 3}"))];

        // act
        await PublishAsync(Source(), events);

        // assert - every event is delivered once, and the events of each order in the order they were published
        var deliveries = await endpoint.WaitForAsync(12);
        Assert.Equal(events.Select(e => e.Id).Order(), deliveries.Select(d => d.Id).Order());
        foreach (var key in events.Select(e => e.Subject).Distinct())
            Assert.Equal(events.Where(e => e.Subject == key).Select(e => e.Id), deliveries.Where(d => d.Event.GetProperty("subject").GetString() == key).Select(d => d.Id));
        await WaitForAckedOffsetAsync(Name(), 12);
    }

    [Fact]
    public async Task A_Subscription_Should_Switch_To_Batches_Without_Losing_Or_Repeating_Events()
    {
        // arrange
        var endpoint = fixture.Subscriber.Register(Name());
        await SubscribeAsync(Name(), Source(), endpoint);
        await PublishAsync(Source(), [("before-0", null), ("before-1", null)]);
        await endpoint.WaitForAsync(2);
        await WaitForAckedOffsetAsync(Name(), 2);

        // act
        await fixture.Api.Resources.Subscriptions.PatchAsync(new Patch(PatchType.JsonMergePatch, new { spec = new { subscriber = new { batch = new { maxSize = 10 } } } }), Name());
        await Task.Delay(1000);
        await PublishAsync(Source(), [("after-0", null), ("after-1", null), ("after-2", null)]);

        // assert
        var deliveries = await endpoint.WaitForAsync(5);
        await WaitForAckedOffsetAsync(Name(), 5);
        await Task.Delay(500);
        Assert.Equal(["before-0", "before-1", "after-0", "after-1", "after-2"], endpoint.Deliveries.Select(d => d.Id));
        Assert.All(endpoint.Deliveries.Skip(2), d => Assert.True(d.Batched));
    }

}
