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
using Json.Patch;
using Microsoft.Extensions.Logging.Abstractions;
using Neuroglia.Data.Expressions.Services;

namespace CloudStreams.UnitTests.Cases.Broker;

/// <summary>
/// Covers the components the subscription handler delegates to, on their own (see #163)
/// </summary>
public class DeliveryComponentTests
{

    static HttpRequestException Failure(HttpStatusCode? status = null) => new("failed", null, status);

    [Fact]
    public void The_Retry_Schedule_Should_Be_Exhausted_After_The_Max_Attempts()
    {
        var schedule = new DeliveryRetrySchedule(new() { MaxAttempts = 2, BackoffDuration = RetryBackoffDuration.Constant(Duration.FromSeconds(1)) });
        Assert.False(schedule.IsExhausted);
        Assert.Equal(TimeSpan.FromSeconds(1), schedule.Next(out _));
        Assert.False(schedule.IsExhausted);
        schedule.Next(out _);
        Assert.True(schedule.IsExhausted);
        Assert.Equal(2, schedule.Attempts);
    }

    [Fact]
    public void The_Retry_Schedule_Should_Never_Be_Exhausted_When_Retrying_Forever()
    {
        var schedule = new DeliveryRetrySchedule(new() { MaxAttempts = -1, BackoffDuration = RetryBackoffDuration.Constant(Duration.FromSeconds(0)) });
        for (var i = 0; i < 100; i++) schedule.Next(out _);
        Assert.False(schedule.IsExhausted);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData(HttpStatusCode.ServiceUnavailable, true)]
    [InlineData(HttpStatusCode.BadRequest, false)]
    public void The_Retry_Schedule_Should_Only_Retry_The_Configured_Status_Codes_And_Failures_Without_Status(HttpStatusCode? status, bool retryable)
    {
        var schedule = new DeliveryRetrySchedule(new() { StatusCodes = [503], BackoffDuration = RetryBackoffDuration.Constant(Duration.FromSeconds(0)) });
        Assert.Equal(retryable, schedule.IsRetryable(Failure(status)));
    }

    [Fact]
    public void The_Circuit_Should_Break_After_The_Configured_Consecutive_Failures()
    {
        // arrange
        var schedule = new DeliveryRetrySchedule(new()
        {
            MaxAttempts = 10,
            BackoffDuration = RetryBackoffDuration.Constant(Duration.FromMilliseconds(10)),
            CircuitBreaker = new() { BreakAfter = 2, BreakDuration = Duration.FromSeconds(30) }
        });

        // act - the initial failure counts: the first retry follows a single failure, the second one two
        var first = schedule.Next(out var firstBroken);
        schedule.RecordFailure();
        var second = schedule.Next(out var secondBroken);

        // assert
        Assert.False(firstBroken);
        Assert.Equal(TimeSpan.FromMilliseconds(10), first);
        Assert.True(secondBroken);
        Assert.Equal(TimeSpan.FromSeconds(30), second);
        Assert.Equal(0, schedule.ConsecutiveFailures);
    }

    [Fact]
    public async Task An_Acknowledgement_Should_Only_Be_Resolvable_Once_Observed()
    {
        // arrange
        var pending = 0;
        using var coordinator = new AcknowledgementCoordinator(delta => pending += delta, _ => Task.CompletedTask);

        // act and assert
        coordinator.Begin("token", 41, observed: false);
        Assert.Equal(1, pending);
        Assert.False(coordinator.TryGetObserved(out _));
        coordinator.MarkObserved("another-token");
        Assert.False(coordinator.TryGetObserved(out _));
        coordinator.MarkObserved("token");
        Assert.True(coordinator.TryGetObserved(out var offset));
        Assert.Equal(41ul, offset);
        Assert.False(coordinator.End("another-token", out _));
        Assert.True(coordinator.End("token", out var pendingFor));
        Assert.NotNull(pendingFor);
        Assert.Equal(0, pending);
        Assert.False(coordinator.IsAwaiting);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task An_Acknowledgement_Should_Expire_Unless_It_Has_Ended()
    {
        // arrange
        var expired = new List<string>();
        using var coordinator = new AcknowledgementCoordinator(_ => { }, token => { lock (expired) expired.Add(token); return Task.CompletedTask; });
        coordinator.Begin("expires", 1, observed: false);
        coordinator.ScheduleTimeout("expires", DateTimeOffset.UtcNow.AddMilliseconds(50), CancellationToken.None);

        // act
        await Task.Delay(300);
        coordinator.End(null, out _);
        coordinator.Begin("ends", 2, observed: false);
        coordinator.ScheduleTimeout("ends", DateTimeOffset.UtcNow.AddMilliseconds(100), CancellationToken.None);
        coordinator.End("ends", out _);
        await Task.Delay(300);

        // assert
        lock (expired) Assert.Equal(["expires"], expired);
    }

    [Fact]
    public async Task An_Acknowledgement_Restored_After_A_Restart_Should_Not_Report_How_Long_It_Has_Been_Pending()
    {
        using var coordinator = new AcknowledgementCoordinator(_ => { }, _ => Task.CompletedTask);
        coordinator.Begin("token", 1, observed: true);
        Assert.True(coordinator.End(null, out var pendingFor));
        Assert.Null(pendingFor);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task The_Page_Reader_Should_Read_Pages_Until_The_Length_And_Read_The_Next_Page_Ahead()
    {
        // arrange
        var reads = new List<long>();
        var reader = new StreamPageReader((offset, length, _) =>
        {
            lock (reads) reads.Add(offset);
            return Task.FromResult(Enumerable.Range((int)offset, (int)Math.Min(length, 5 - offset)).Select(i => BrokerTestContext.CreateRecord((ulong)i)).ToList());
        }, 2);

        // act
        var pages = new List<int>();
        await foreach (var page in reader.ReadPagesAsync(0, () => 5)) pages.Add(page.Count);

        // assert
        Assert.Equal([2, 2, 1], pages);
        lock (reads) Assert.Equal([0L, 2L, 4L], reads);
    }

    [Fact]
    public async Task The_Page_Reader_Should_Wait_For_Records_That_Are_Not_Readable_Yet()
    {
        // arrange - the record has been appended, but is not readable at the first attempt
        var attempts = 0;
        var reader = new StreamPageReader((offset, _, _) => Task.FromResult(Interlocked.Increment(ref attempts) == 1 ? [] : new List<CloudEventRecord>() { BrokerTestContext.CreateRecord((ulong)offset) }), 10);

        // act
        var pages = await reader.ReadPagesAsync(0, () => 1).ToListAsync();

        // assert
        Assert.Single(pages);
        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task The_Status_Reporter_Should_Reapply_A_Change_To_A_Status_That_Changed_Concurrently()
    {
        // arrange - another writer patched the status in the meantime: the first patch conflicts
        var subscription = new Subscription() { Metadata = new() { Name = "orders", ResourceVersion = "1" }, Spec = new() { Subscriber = new() { Uri = new("http://localhost") } }, Status = new() { Phase = SubscriptionStatusPhase.Active } };
        var current = subscription.Clone()!;
        current.Metadata.ResourceVersion = "2";
        current.Status!.Stream = new() { AckedOffset = 7 };
        var repository = Substitute.For<IResourceRepository>();
        var versions = new List<string?>();
        repository.PatchSubResourceAsync(Arg.Any<Patch>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                versions.Add(call.ArgAt<string?>(7));
                return versions.Count == 1 ? Task.FromException<IResource>(new ProblemDetailsException(new(ProblemTypes.NotFound, "Conflict", (int)HttpStatusCode.Conflict))) : Task.FromResult<IResource>(current);
            });
        repository.GetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult<IResource?>(current));
        var reporter = new SubscriptionStatusReporter(repository, NullLogger.Instance, subscription);

        // act
        var patched = await reporter.PatchAsync(s => s.Status!.Phase = SubscriptionStatusPhase.Suspended);

        // assert - the second attempt is made against the current version
        Assert.True(patched);
        Assert.Equal(["1", "2"], versions);
        Assert.Same(current, reporter.Latest);
    }

    [Fact]
    public async Task The_Status_Reporter_Should_Not_Report_The_Same_Subscriber_State_Twice()
    {
        // arrange
        var subscription = new Subscription() { Metadata = new() { Name = "orders" }, Spec = new() { Subscriber = new() { Uri = new("http://localhost") } }, Status = new() };
        var repository = Substitute.For<IResourceRepository>();
        repository.PatchSubResourceAsync(Arg.Any<Patch>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IResource>(subscription));
        var reporter = new SubscriptionStatusReporter(repository, NullLogger.Instance, subscription);

        // act
        await reporter.SetSubscriberStateAsync(subscription, SubscriberState.Unreachable, "down");
        await reporter.SetSubscriberStateAsync(subscription, SubscriberState.Unreachable, "down");
        await reporter.SetSubscriberStateAsync(subscription, SubscriberState.Reachable);

        // assert
        Assert.Equal(2, repository.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IResourceRepository.PatchSubResourceAsync)));
    }

    [Fact]
    public async Task The_Pipeline_Should_Match_Attribute_Filters_As_Regular_Expressions()
    {
        // arrange
        var pipeline = new SubscriptionEventPipeline(Substitute.For<IExpressionEvaluator>(), [], new HttpClient(), Neuroglia.Serialization.Json.JsonSerializer.Default);
        var subscription = new Subscription() { Metadata = new() { Name = "orders" }, Spec = new() { Subscriber = new() { Uri = new("http://localhost") }, Filter = new() { Type = CloudEventFilterType.Attributes, Attributes = new Dictionary<string, string?>() { ["type"] = "^com\\.cloud-streams\\.test" } } } };

        // act and assert
        Assert.True(await pipeline.FiltersAsync(subscription, BrokerTestContext.CreateRecord(1)));
        Assert.False(await pipeline.FiltersAsync(subscription, BrokerTestContext.CreateRecord(1, "com.other.v1")));
    }

    [Fact]
    public async Task The_Pipeline_Should_Copy_Cloud_Events_The_Subscription_Does_Not_Mutate()
    {
        var pipeline = new SubscriptionEventPipeline(Substitute.For<IExpressionEvaluator>(), [], new HttpClient(), Neuroglia.Serialization.Json.JsonSerializer.Default);
        var subscription = new Subscription() { Metadata = new() { Name = "orders" }, Spec = new() { Subscriber = new() { Uri = new("http://localhost") } } };
        var e = new CloudEvent() { Id = "1", SpecVersion = "1.0", Source = new("https://a.example.com"), Type = "a" };
        var mutated = await pipeline.MutateAsync(subscription, e);
        Assert.NotSame(e, mutated);
        Assert.Equal(e.Id, mutated.Id);
    }

}
