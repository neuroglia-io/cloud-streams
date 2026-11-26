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
using Neuroglia.Data.Infrastructure.ResourceOriented;
using Neuroglia.Eventing.CloudEvents;
using System.Globalization;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace CloudStreams.IntegrationTests.Cases;

/// <summary>
/// Verifies, end to end, how operators recover subscriptions: replaying them from a point in time, and dead-lettering the events they fail to deliver (#207, #211)
/// </summary>
[Collection(CloudStreamsCollection.Name)]
public sealed class SubscriptionRecoveryTests(CloudStreamsFixture fixture, ITestOutputHelper output)
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

    async Task PublishAsync(Uri source, params string[] ids)
    {
        foreach (var id in ids)
        {
            var json = $"{{\"specversion\":\"1.0\",\"id\":\"{id}\",\"source\":\"{source}\",\"type\":\"io.cloud-streams.e2e.v1\",\"data\":{{\"id\":\"{id}\"}}}}";
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

    /// <summary>
    /// Publishes 3 events, then 3 more, and returns a time between the two batches
    /// </summary>
    async Task<DateTimeOffset> PublishTwoBatchesAsync(Uri source)
    {
        await PublishAsync(source, "before-0", "before-1", "before-2");
        // the store's clock may differ from the test's slightly: the time between the batches is taken well after the first one, and well before the second one
        await Task.Delay(1500);
        var between = DateTimeOffset.UtcNow;
        await Task.Delay(1500);
        await PublishAsync(source, "after-0", "after-1", "after-2");
        await fixture.WaitForPartitionAsync(CloudEventPartitionType.BySource, source.OriginalString, 6);
        return between;
    }

    [Fact]
    public async Task The_Stream_Should_Be_Readable_From_A_Point_In_Time()
    {
        // arrange
        var between = await PublishTwoBatchesAsync(Source());
        var time = Uri.EscapeDataString(between.ToString("O", CultureInfo.InvariantCulture));
        var partition = $"partition.type=by-source&partition.id={Uri.EscapeDataString(Source().OriginalString)}";

        // act
        using var forwards = await fixture.Gateway.GetAsync($"api/core/v1/cloud-events/stream/read?{partition}&time={time}&direction=forwards");
        using var backwards = await fixture.Gateway.GetAsync($"api/core/v1/cloud-events/stream/read?{partition}&time={time}&direction=backwards");
        using var both = await fixture.Gateway.GetAsync($"api/core/v1/cloud-events/stream/read?{partition}&time={time}&offset=0");

        // assert
        Assert.Equal(HttpStatusCode.OK, forwards.StatusCode);
        Assert.Equal(["after-0", "after-1", "after-2"], JsonSerializer.Deserialize<JsonElement[]>(await forwards.Content.ReadAsStringAsync())!.Select(e => e.GetProperty("id").GetString()));
        Assert.Equal(["before-2", "before-1", "before-0"], JsonSerializer.Deserialize<JsonElement[]>(await backwards.Content.ReadAsStringAsync())!.Select(e => e.GetProperty("id").GetString()));
        Assert.Equal(HttpStatusCode.BadRequest, both.StatusCode);
    }

    [Fact]
    public async Task A_Subscription_Should_Be_Replayable_From_A_Point_In_Time()
    {
        // arrange
        var between = await PublishTwoBatchesAsync(Source());
        var endpoint = fixture.Subscriber.Register(Name());
        await SubscribeAsync(Name(), Source(), endpoint);
        await endpoint.WaitForAsync(6);

        // act
        var replayed = await fixture.Api.Resources.Subscriptions.ReplayAsync(Name(), between);

        // assert - the offset of the first event recorded after the time, in the partition the subscription consumes
        Assert.Equal(3, replayed.Spec.Stream!.Offset);
        var deliveries = await endpoint.WaitForAsync(9);
        Assert.Equal(["after-0", "after-1", "after-2"], deliveries.Skip(6).Select(d => d.Id));
    }

    [Fact]
    public async Task Events_That_Cannot_Be_Delivered_Should_Be_Dead_Lettered_And_Redeliverable()
    {
        // arrange - the subscriber refuses 'poison' until it is fixed
        var fixedSubscriber = false;
        var endpoint = fixture.Subscriber.Register(Name(), d => d.Id == "poison" && !Volatile.Read(ref fixedSubscriber) ? 400 : 200);
        await SubscribeAsync(Name(), Source(), endpoint, spec =>
        {
            spec.Subscriber.RetryPolicy = new() { MaxAttempts = 0, BackoffDuration = RetryBackoffDuration.Constant(Duration.FromSeconds(0)) };
            spec.DeadLetter = new();
        });

        // act
        await PublishAsync(Source(), "event-0", "poison", "event-2");

        // assert - the subscription carries on after the poison event, which is set aside
        var deliveries = await endpoint.WaitForAsync(3);
        Assert.Equal(["event-0", "poison", "event-2"], deliveries.Select(d => d.Id));
        var deadLetters = await WaitForDeadLettersAsync(Name(), 1);
        var deadLetter = Assert.Single(deadLetters);
        Assert.Equal("poison", deadLetter.Event.Id);
        Assert.Equal(1ul, deadLetter.EventOffset);
        Assert.Contains("BadRequest", deadLetter.Reason.Detail);
        var subscription = await fixture.Api.Resources.Subscriptions.GetAsync(Name());
        Assert.Null(subscription.Status?.Stream?.Fault);

        // act - the subscriber refuses the redelivery, then accepts it once fixed
        var refused = await Assert.ThrowsAsync<ProblemDetailsException>(() => fixture.Api.Resources.Subscriptions.RedeliverDeadLetterAsync(Name(), deadLetter.Offset));
        Volatile.Write(ref fixedSubscriber, true);
        var redelivery = await fixture.Api.Resources.Subscriptions.RedeliverDeadLetterAsync(Name(), deadLetter.Offset);
        var missing = await Assert.ThrowsAsync<ProblemDetailsException>(() => fixture.Api.Resources.Subscriptions.RedeliverDeadLetterAsync(Name(), 42));

        // assert
        Assert.Equal((int)HttpStatusCode.BadGateway, refused.Problem.Status);
        Assert.Equal(200, redelivery.Status);
        Assert.Equal("poison", endpoint.Deliveries[^1].Id);
        Assert.Equal((int)HttpStatusCode.NotFound, missing.Problem.Status);
    }

    [Fact]
    public async Task The_Event_That_Faulted_A_Subscription_Should_Be_Skippable()
    {
        // arrange - without a dead-letter policy, the poison event faults the subscription
        var endpoint = fixture.Subscriber.Register(Name(), d => d.Id == "poison" ? 400 : 200);
        await SubscribeAsync(Name(), Source(), endpoint, spec => spec.Subscriber.RetryPolicy = new() { MaxAttempts = 0, BackoffDuration = RetryBackoffDuration.Constant(Duration.FromSeconds(0)) });
        await PublishAsync(Source(), "event-0", "poison", "event-2");
        var faulted = await WaitForSubscriptionAsync(Name(), s => s.Status?.Stream?.Fault != null);
        Assert.Equal(1ul, faulted.Status!.Stream!.FaultedOffset);

        // act
        var stale = await Assert.ThrowsAsync<ProblemDetailsException>(() => fixture.Api.Resources.Subscriptions.SkipAsync(Name(), 0));
        await fixture.Api.Resources.Subscriptions.SkipAsync(Name(), 1);

        // assert - delivery resumes after the poison event, which is not delivered again
        Assert.Equal((int)HttpStatusCode.Conflict, stale.Problem.Status);
        var deliveries = await endpoint.WaitForAsync(3);
        Assert.Equal(["event-0", "poison", "event-2"], deliveries.Select(d => d.Id));
        var skipped = await WaitForSubscriptionAsync(Name(), s => s.Status?.Stream is { Fault: null, AckedOffset: 3 });
        Assert.Null(skipped.Status!.Stream!.FaultedOffset);
    }

    [Fact]
    public async Task Recorded_Events_Should_Be_Described_By_Activity_Partitions_And_Tests()
    {
        // arrange
        await PublishAsync(Source(), "event-0", "event-1", "event-2");

        // act
        var activity = await fixture.Api.CloudEvents.Stream.GetStreamActivityAsync(TimeSpan.FromHours(1), 6, 1000);
        var partitions = await fixture.Api.CloudEvents.Partitions.ListPartitionMetadataByTypeAsync(CloudEventPartitionType.BySource, Source().OriginalString);
        var test = await fixture.Api.Resources.Subscriptions.TestAsync(new()
        {
            Partition = new(CloudEventPartitionType.BySource, Source().OriginalString),
            Filter = new() { Type = CloudEventFilterType.Expression, Expression = "${ .data.id != \"event-1\" }" },
            Mutation = new() { Type = CloudEventMutationType.Expression, Expression = "${ . + { subject: \"tested\" } }" }
        });

        // assert
        Assert.True(activity.Buckets[^1].Count >= 3);
        Assert.Contains(activity.Sources, s => s.Value == Source().OriginalString && s.Count == 3);
        var partition = Assert.Single(partitions.Items);
        Assert.Equal(3ul, partition.Length);
        Assert.True(partition.LastEvent > DateTimeOffset.UtcNow.AddMinutes(-5));
        Assert.Equal([2ul, 1ul, 0ul], test.Items.Select(i => i.Offset!.Value));
        Assert.Equal([true, false, true], test.Items.Select(i => i.Matched));
        Assert.All(test.Items.Where(i => i.Matched), i => Assert.Equal("tested", i.Mutated!.Subject));
    }

    async Task<Subscription> WaitForSubscriptionAsync(string name, Func<Subscription, bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (true)
        {
            var subscription = await fixture.Api.Resources.Subscriptions.GetAsync(name);
            if (condition(subscription)) return subscription;
            if (DateTime.UtcNow > deadline) throw new TimeoutException($"The subscription '{name}' did not reach the expected state in time");
            await Task.Delay(250);
        }
    }

    async Task<IReadOnlyList<DeadLetter>> WaitForDeadLettersAsync(string name, int count)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (true)
        {
            var deadLetters = await fixture.Api.Resources.Subscriptions.GetDeadLettersAsync(name);
            if (deadLetters.Count >= count || DateTime.UtcNow > deadline) return deadLetters;
            await Task.Delay(250);
        }
    }

}
