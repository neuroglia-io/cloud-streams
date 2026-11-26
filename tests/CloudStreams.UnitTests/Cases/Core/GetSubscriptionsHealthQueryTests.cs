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

using CloudStreams.Core.Application.Queries.Subscriptions;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json.Nodes;

namespace CloudStreams.UnitTests.Cases.Core;

/// <summary>
/// Covers the subscription health endpoint requested by issue #128
/// </summary>
public class GetSubscriptionsHealthQueryTests
{

    static Subscription CreateSubscription(string name, Action<Subscription>? configure = null)
    {
        var subscription = new Subscription()
        {
            Metadata = new() { Name = name, Generation = 1 },
            Spec = new() { Subscriber = new() { Endpoint = new Uri("http://localhost:8888/events") }, Stream = new() { Offset = 0 } },
            Status = new() { ObservedGeneration = 1, Phase = SubscriptionStatusPhase.Active, Stream = new(), Subscriber = new() }
        };
        configure?.Invoke(subscription);
        return subscription;
    }

    static Task<List<SubscriptionHealth>> ExecuteAsync(int streamLength, params Subscription[] subscriptions) => ExecuteAsync(streamLength, 0, subscriptions);

    static async Task<List<SubscriptionHealth>> ExecuteAsync(int streamLength, ulong expired, params Subscription[] subscriptions)
    {
        var repository = Substitute.For<IResourceRepository>();
        repository.GetAllAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<IEnumerable<LabelSelector>?>(), Arg.Any<CancellationToken>())
            .Returns(subscriptions.ToAsyncEnumerable().Cast<IResource>());
        using var eventStore = new InMemoryCloudEventStore().Seed(streamLength);
        if (expired > 0) await eventStore.TruncateAsync(expired);
        var handler = new GetSubscriptionsHealthQueryHandler(NullLogger<GetSubscriptionsHealthQueryHandler>.Instance, repository, eventStore);
        var result = await handler.HandleAsync(new GetSubscriptionsHealthQuery(null, null), CancellationToken.None);
        return [.. result.Data!];
    }

    [Fact]
    public async Task Health_Should_Compute_The_Lag_Of_Each_Subscription()
    {
        // act
        var health = await ExecuteAsync(10,
            CreateSubscription("healthy", s => { s.Status!.Stream!.AckedOffset = 7; s.Status.Subscriber!.State = SubscriberState.Reachable; }),
            CreateSubscription("partitioned", s => { s.Spec.Partition = new(CloudEventPartitionType.BySource, "https://test.com"); s.Status!.Stream!.AckedOffset = 10; }));

        // assert
        Assert.Equal(2, health.Count);
        var healthy = health.Single(h => h.Name == "healthy");
        Assert.Equal("active", healthy.Phase);
        Assert.Equal(7ul, healthy.AckedOffset);
        Assert.Equal(10ul, healthy.PartitionLength);
        Assert.Equal(3ul, healthy.Lag);
        Assert.Equal("reachable", healthy.SubscriberState);
        var partitioned = health.Single(h => h.Name == "partitioned");
        Assert.Equal("https://test.com", partitioned.PartitionId);
        Assert.Equal(0ul, partitioned.Lag);
        Assert.Null(partitioned.SubscriberState);
    }

    [Fact]
    public async Task Health_Should_Report_The_Events_That_Expired_Before_A_Subscription_Delivered_Them()
    {
        // arrange - the first 4 of 10 events have expired (see #206)
        var behind = CreateSubscription("behind", s => s.Status!.Stream!.AckedOffset = 1);
        var starting = CreateSubscription("starting");
        var ahead = CreateSubscription("ahead", s => s.Status!.Stream!.AckedOffset = 6);
        var live = CreateSubscription("live", s => s.Spec.Stream = null);

        // act
        var health = await ExecuteAsync(10, 4, behind, starting, ahead, live);

        // assert
        Assert.Equal([3ul, 4ul, null, null], health.Select(h => h.ExpiredEvents));
    }

    [Fact]
    public async Task Health_Should_Report_Faulted_Subscriptions_As_Failed_With_The_Fault_As_Reason()
    {
        // act
        var health = await ExecuteAsync(10, CreateSubscription("faulted", s => s.Status!.Stream!.Fault = new(new("https://cloud-streams.io/tests/fault"), "Delivery failed", 500, "Failed after 6 attempts")));

        // assert
        Assert.Equal("failed", health.Single().Phase);
        Assert.Equal("Failed after 6 attempts", health.Single().SubscriberReason);
    }

    [Fact]
    public async Task Health_Should_Serialize_As_A_Root_Array_With_Every_Field_Present()
    {
        // arrange
        var health = await ExecuteAsync(0, CreateSubscription("inactive", s => { s.Spec.Stream = null; s.Status = null; }));

        // act
        var json = JsonNode.Parse(Neuroglia.Serialization.Json.JsonSerializer.Default.SerializeToText(health))!;

        // assert
        var item = Assert.IsType<JsonObject>(Assert.Single(json.AsArray()));
        foreach (var field in new[] { "name", "namespace", "phase", "partitionId", "ackedOffset", "partitionLength", "lag", "subscriberState", "subscriberReason" }) Assert.True(item.ContainsKey(field), $"Missing field '{field}' in {item.ToJsonString()}");
        Assert.Equal("inactive", item["phase"]!.GetValue<string>());
        Assert.Null(item["lag"]);
    }

}
