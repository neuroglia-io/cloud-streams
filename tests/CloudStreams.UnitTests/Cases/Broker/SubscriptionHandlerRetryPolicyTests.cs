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

namespace CloudStreams.UnitTests.Cases.Broker;

/// <summary>
/// Regression guards for issues #121 and #131: the subscriber retry policy must actually govern redelivery
/// </summary>
public class SubscriptionHandlerRetryPolicyTests
{

    static HttpClientRetryPolicy Policy(int? maxAttempts, TimeSpan? backoff = null, CircuitBreakerPolicy? circuitBreaker = null, params int[] statusCodes) => new()
    {
        MaxAttempts = maxAttempts,
        BackoffDuration = RetryBackoffDuration.Constant(Duration.FromTimeSpan(backoff ?? TimeSpan.Zero)),
        CircuitBreaker = circuitBreaker,
        StatusCodes = statusCodes.Length == 0 ? null : [.. statusCodes]
    };

    [Fact]
    public async Task Max_Attempts_Should_Bound_Redelivery_And_Fault_The_Subscription()
    {
        // arrange
        await using var context = await BrokerTestContext.CreateAsync(StubHttpMessageHandler.AlwaysResponds(HttpStatusCode.InternalServerError),
            configureSubscription: s => s.Spec.Subscriber.RetryPolicy = Policy(3));

        // act
        await context.Handler.DispatchRecordAsync(BrokerTestContext.CreateRecord(41)).WaitAsync(TimeSpan.FromSeconds(5));

        // assert - the initial attempt plus three retries, each failure counted exactly once
        Assert.Equal(4, context.Http.AttemptCount);
        Assert.Equal(4, context.Metrics.TotalDeliveryFailures);
        var fault = context.Patches.Operations.Single(o => o.Path.ToString() == "/status/stream/fault").Value!.ToString();
        Assert.Contains("after 4 attempt(s)", fault);
        Assert.Contains("to skip it", fault);
        // the offset of the event is recorded with the fault, so that it can be skipped (see #224)
        Assert.Equal("41", context.Patches.Operations.Single(o => o.Path.ToString() == "/status/stream/faultedOffset").Value!.ToString());
        Assert.False(context.Patches.Touched("/status/stream/ackedOffset"));
    }

    [Fact]
    public async Task Unset_Max_Attempts_Should_Default_To_A_Bounded_Value()
    {
        // arrange
        await using var context = await BrokerTestContext.CreateAsync(StubHttpMessageHandler.AlwaysResponds(HttpStatusCode.InternalServerError),
            configureSubscription: s => s.Spec.Subscriber.RetryPolicy = Policy(null));

        // act
        await context.Handler.DispatchRecordAsync(BrokerTestContext.CreateRecord(41)).WaitAsync(TimeSpan.FromSeconds(5));

        // assert
        Assert.Equal(1 + RetryPolicy.DefaultMaxAttempts, context.Http.AttemptCount);
        Assert.True(context.Patches.Touched("/status/stream/fault"));
    }

    [Fact]
    public async Task Negative_Max_Attempts_Should_Retry_Until_Delivered()
    {
        // arrange
        await using var context = await BrokerTestContext.CreateAsync(StubHttpMessageHandler.FailsThenSucceeds(12),
            configureSubscription: s => s.Spec.Subscriber.RetryPolicy = Policy(-1));

        // act
        await context.Handler.DispatchRecordAsync(BrokerTestContext.CreateRecord(41)).WaitAsync(TimeSpan.FromSeconds(5));

        // assert
        Assert.Equal(13, context.Http.AttemptCount);
        Assert.Contains(42ul, context.Patches.OffsetsWrittenTo());
        Assert.False(context.Patches.Touched("/status/stream/fault"));
    }

    [Fact]
    public async Task Backoff_Duration_Should_Be_Honoured_Between_Attempts()
    {
        // arrange
        var backoff = TimeSpan.FromMilliseconds(150);
        await using var context = await BrokerTestContext.CreateAsync(StubHttpMessageHandler.FailsThenSucceeds(2),
            configureSubscription: s => s.Spec.Subscriber.RetryPolicy = Policy(5, backoff));

        // act
        await context.Handler.DispatchRecordAsync(BrokerTestContext.CreateRecord(41)).WaitAsync(TimeSpan.FromSeconds(5));

        // assert
        Assert.Equal(3, context.Http.AttemptCount);
        Assert.All(context.Http.IntervalsBetweenAttempts.Skip(1), interval => Assert.True(interval >= backoff - TimeSpan.FromMilliseconds(20), $"Expected at least {backoff} between attempts, got {interval}"));
    }

    [Fact]
    public async Task Circuit_Breaker_Should_Pause_Delivery_After_Consecutive_Failures()
    {
        // arrange
        var breakDuration = TimeSpan.FromMilliseconds(300);
        await using var context = await BrokerTestContext.CreateAsync(StubHttpMessageHandler.FailsThenSucceeds(3),
            configureSubscription: s => s.Spec.Subscriber.RetryPolicy = Policy(5, circuitBreaker: new(2, Duration.FromTimeSpan(breakDuration))));

        // act
        await context.Handler.DispatchRecordAsync(BrokerTestContext.CreateRecord(41)).WaitAsync(TimeSpan.FromSeconds(5));

        // assert - attempts 1 and 2 fail, which breaks the circuit before attempt 3
        Assert.Equal(4, context.Http.AttemptCount);
        var intervals = context.Http.IntervalsBetweenAttempts;
        Assert.True(intervals[1] < breakDuration, $"The circuit should not be broken after a single failure, got {intervals[1]}");
        Assert.True(intervals[2] >= breakDuration - TimeSpan.FromMilliseconds(20), $"Expected the circuit to be broken for at least {breakDuration}, got {intervals[2]}");
    }

    [Fact]
    public async Task Status_Codes_Not_Configured_For_Retry_Should_Fault_Without_Retrying()
    {
        // arrange
        await using var context = await BrokerTestContext.CreateAsync(StubHttpMessageHandler.AlwaysResponds(HttpStatusCode.BadRequest),
            configureSubscription: s => s.Spec.Subscriber.RetryPolicy = Policy(5, statusCodes: 503));

        // act
        await context.Handler.DispatchRecordAsync(BrokerTestContext.CreateRecord(41)).WaitAsync(TimeSpan.FromSeconds(5));

        // assert
        Assert.Equal(1, context.Http.AttemptCount);
        Assert.Contains("not configured to retry", context.Patches.Operations.Single(o => o.Path.ToString() == "/status/stream/fault").Value!.ToString());
    }

    [Fact]
    public async Task Exhausted_Delivery_Without_Stream_Tracking_Should_Drop_The_Event_And_Keep_Going()
    {
        // arrange
        await using var context = await BrokerTestContext.CreateAsync(StubHttpMessageHandler.Responds(HttpStatusCode.InternalServerError, HttpStatusCode.InternalServerError, HttpStatusCode.OK),
            configureSubscription: s =>
            {
                s.Spec.Stream = null;
                s.Spec.Subscriber.RetryPolicy = Policy(1);
            });

        // act
        await context.Handler.HandleCloudEventAsync(BrokerTestContext.CreateRecord(41));
        await context.Handler.HandleCloudEventAsync(BrokerTestContext.CreateRecord(42));

        // assert
        Assert.False(context.Patches.Touched("/status/stream/fault"));
        Assert.False(context.Handler.IsOutOfSync);
        Assert.Equal(3, context.Http.AttemptCount);
        Assert.Equal(1, context.Metrics.TotalPublishedEvents);
    }

}
