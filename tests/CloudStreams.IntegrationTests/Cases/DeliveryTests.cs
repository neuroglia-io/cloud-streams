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
using Neuroglia.Data.Infrastructure.EventSourcing;
using Neuroglia.Data.Infrastructure.ResourceOriented;
using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace CloudStreams.IntegrationTests.Cases;

/// <summary>
/// Publishes events to a gateway, and verifies that a broker delivers them to subscribers as documented: in order, at least once, retried, caught up and
/// acknowledged (#204)
/// </summary>
[Collection(CloudStreamsCollection.Name)]
public sealed class DeliveryTests(CloudStreamsFixture fixture, ITestOutputHelper output)
    : IAsyncLifetime
{

    // Each test publishes events of its own source, and subscribes to that source only, so that the tests are independent of each other
    static Uri Source([CallerMemberName] string test = "") => new($"https://e2e.cloud-streams.io/{test.ToLowerInvariant()}");

    static string Name([CallerMemberName] string test = "") => test.ToLowerInvariant().Replace('_', '-');

    IReadOnlyList<int>? _logs;

    public Task InitializeAsync()
    {
        this._logs = fixture.CheckpointLogs();
        return Task.CompletedTask;
    }

    // the logs written by the gateway and the broker during the test explain a failure, and cost nothing to keep otherwise
    public Task DisposeAsync()
    {
        output.WriteLine(fixture.GetLogs(this._logs));
        return Task.CompletedTask;
    }

    async Task PublishAsync(Uri source, int count, string idPrefix = "event")
    {
        for (var i = 0; i < count; i++)
        {
            var json = $"{{\"specversion\":\"1.0\",\"id\":\"{idPrefix}-{i}\",\"source\":\"{source}\",\"type\":\"io.cloud-streams.e2e.v1\",\"datacontenttype\":\"application/json\",\"data\":{{\"index\":{i}}}}}";
            using var response = await fixture.Gateway.PostAsync("api/events/pub", new StringContent(json, Encoding.UTF8, "application/cloudevents+json"));
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }
    }

    async Task<Subscription> SubscribeAsync(string name, Uri source, TestSubscriber.Endpoint endpoint, Action<SubscriptionSpec>? configure = null)
    {
        var spec = new SubscriptionSpec()
        {
            Partition = new(CloudEventPartitionType.BySource, source.OriginalString),
            Subscriber = new() { Endpoint = endpoint.Uri },
            Stream = new() { Offset = 0 }
        };
        configure?.Invoke(spec);
        return await fixture.Api.Resources.Subscriptions.CreateAsync(new Subscription() { Metadata = new() { Name = name }, Spec = spec });
    }

    async Task<Subscription> WaitForAsync(string name, Func<Subscription, bool> condition, string description, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(30));
        Subscription subscription;
        do
        {
            subscription = await fixture.Api.Resources.Subscriptions.GetAsync(name);
            if (condition(subscription)) return subscription;
            await Task.Delay(250);
        }
        while (DateTime.UtcNow < deadline);
        throw new TimeoutException($"The subscription '{name}' did not reach the expected state ({description}): {Neuroglia.Serialization.Json.JsonSerializer.Default.SerializeToText(subscription.Status)}");
    }

    static string[] Ids(IEnumerable<TestSubscriber.Delivery> deliveries) => [.. deliveries.Select(d => d.Id)];

    static string[] Expected(int count, string idPrefix = "event") => [.. Enumerable.Range(0, count).Select(i => $"{idPrefix}-{i}")];

    [Fact]
    public async Task Published_Events_Should_Be_Delivered_In_Order_And_Acknowledged()
    {
        // arrange
        var endpoint = fixture.Subscriber.Register(Name());
        await SubscribeAsync(Name(), Source(), endpoint);

        // act
        await PublishAsync(Source(), 5);

        // assert
        Assert.Equal(Expected(5), Ids(await endpoint.WaitForAsync(5)));
        Assert.Equal(["0", "1", "2", "3", "4"], endpoint.Deliveries.Select(d => d.Offset));
        await WaitForAsync(Name(), s => s.Status?.Stream?.AckedOffset == 5, "all 5 events acknowledged");
        await Task.Delay(1000);
        Assert.Equal(5, endpoint.Deliveries.Count);
    }

    [Fact]
    public async Task A_New_Subscription_Should_Catch_Up_With_Events_Published_Before_It()
    {
        // arrange
        await PublishAsync(Source(), 5, "past");
        var endpoint = fixture.Subscriber.Register(Name());

        // act
        await SubscribeAsync(Name(), Source(), endpoint);
        await endpoint.WaitForAsync(5);
        await PublishAsync(Source(), 3, "live");

        // assert: the past events are caught up with, then the live ones follow, each once and in order
        var all = await endpoint.WaitForAsync(8);
        Assert.Equal([.. Expected(5, "past"), .. Expected(3, "live")], Ids(all));
        await WaitForAsync(Name(), s => s.Status?.Stream?.AckedOffset == 8, "all 8 events acknowledged");
    }

    [Fact]
    public async Task A_Subscription_Starting_At_The_End_Of_Its_Stream_Should_Only_Receive_New_Events()
    {
        // arrange
        await PublishAsync(Source(), 3, "past");
        // the end of the stream is resolved in the partition, which must hold the past events by then
        await fixture.WaitForPartitionAsync(CloudEventPartitionType.BySource, Source().OriginalString, 3);
        var endpoint = fixture.Subscriber.Register(Name());
        await SubscribeAsync(Name(), Source(), endpoint, spec => spec.Stream = new() { Offset = StreamPosition.EndOfStream });
        await WaitForAsync(Name(), s => s.Status?.Phase == SubscriptionStatusPhase.Active, "active");
        await Task.Delay(2000);

        // act
        await PublishAsync(Source(), 2, "live");

        // assert
        Assert.Equal(Expected(2, "live"), Ids(await endpoint.WaitForAsync(2)));
    }

    [Fact]
    public async Task Failed_Deliveries_Should_Be_Retried_Without_Skipping_Or_Reordering_Events()
    {
        // arrange: every event is refused twice before being accepted
        var attempts = new Dictionary<string, int>();
        var endpoint = fixture.Subscriber.Register(Name(), delivery =>
        {
            lock (attempts)
            {
                attempts[delivery.Id] = attempts.GetValueOrDefault(delivery.Id) + 1;
                return attempts[delivery.Id] <= 2 ? 503 : 200;
            }
        });
        await SubscribeAsync(Name(), Source(), endpoint, spec => spec.Subscriber.RetryPolicy = new()
        {
            StatusCodes = [503],
            BackoffDuration = RetryBackoffDuration.Constant(Duration.FromMilliseconds(200)),
            MaxAttempts = 5
        });

        // act
        await PublishAsync(Source(), 3);

        // assert
        var deliveries = await endpoint.WaitForAsync(9);
        Assert.Equal(["event-0", "event-0", "event-0", "event-1", "event-1", "event-1", "event-2", "event-2", "event-2"], Ids(deliveries));
        await WaitForAsync(Name(), s => s.Status?.Stream?.AckedOffset == 3, "all 3 events acknowledged");
    }

    [Fact]
    public async Task A_Delivery_Refused_With_A_Status_Code_That_Is_Not_Retried_Should_Fault_The_Subscription()
    {
        // arrange
        var endpoint = fixture.Subscriber.Register(Name(), delivery => delivery.Id == "event-1" ? 400 : 200);
        await SubscribeAsync(Name(), Source(), endpoint, spec => spec.Subscriber.RetryPolicy = new()
        {
            StatusCodes = [503],
            BackoffDuration = RetryBackoffDuration.Constant(Duration.FromMilliseconds(200)),
            MaxAttempts = 3
        });

        // act
        await PublishAsync(Source(), 3);

        // assert: the first event is acknowledged, the second faults the subscription, and the third is never delivered
        var subscription = await WaitForAsync(Name(), s => s.Status?.Stream?.Fault != null, "faulted");
        Assert.Equal(1ul, subscription.Status!.Stream!.AckedOffset);
        await Task.Delay(1000);
        Assert.Equal(["event-0", "event-1"], Ids(endpoint.Deliveries));

        // act: resume from the failed event, which the subscriber now accepts
        var resumed = fixture.Subscriber.Register(Name() + "-resumed");
        await fixture.Api.Resources.Subscriptions.PatchAsync(new Patch(PatchType.JsonMergePatch, new { spec = new { subscriber = new { endpoint = resumed.Uri }, stream = new { offset = 1 } } }), Name());

        // assert
        Assert.Equal(["event-1", "event-2"], Ids(await resumed.WaitForAsync(2)));
        await WaitForAsync(Name(), s => s.Status?.Stream?.AckedOffset == 3 && s.Status.Stream.Fault == null, "resumed and caught up");
    }

    [Fact]
    public async Task A_Deferred_Acknowledgement_Should_Suspend_Delivery_Until_The_Subscriber_Calls_Back()
    {
        // arrange: the subscriber defers the acknowledgement of every first delivery of an event
        var seen = new HashSet<string>();
        var endpoint = fixture.Subscriber.Register(Name(), delivery => { lock (seen) return seen.Add(delivery.Id) ? 202 : 200; });
        await SubscribeAsync(Name(), Source(), endpoint);

        // act
        await PublishAsync(Source(), 2);
        var first = (await endpoint.WaitForAsync(1))[0];
        await Task.Delay(2000);

        // assert: nothing else is delivered while the acknowledgement is pending
        Assert.Single(endpoint.Deliveries);
        Assert.False(string.IsNullOrWhiteSpace(first.AckToken));

        // act: acknowledge the first event
        using (var ack = await fixture.Gateway.PostAsJsonAsync($"api/resources/v1/subscriptions/{Name()}/ack", new { token = first.AckToken }))
            Assert.Equal(HttpStatusCode.NoContent, ack.StatusCode);
        var second = (await endpoint.WaitForAsync(2))[1];
        Assert.Equal("event-1", second.Id);

        // act: reject the second one, which is then redelivered
        using (var nack = await fixture.Gateway.PostAsJsonAsync($"api/resources/v1/subscriptions/{Name()}/nack", new { token = second.AckToken, reason = "not yet" }))
            Assert.Equal(HttpStatusCode.NoContent, nack.StatusCode);

        // assert
        Assert.Equal(["event-0", "event-1", "event-1"], Ids(await endpoint.WaitForAsync(3)));
        await WaitForAsync(Name(), s => s.Status?.Stream?.AckedOffset == 2, "both events acknowledged");

        // a token can only be used once
        using var reused = await fixture.Gateway.PostAsJsonAsync($"api/resources/v1/subscriptions/{Name()}/ack", new { token = first.AckToken });
        Assert.Equal(HttpStatusCode.Conflict, reused.StatusCode);
    }

}
