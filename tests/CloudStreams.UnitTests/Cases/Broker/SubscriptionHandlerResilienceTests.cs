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
/// Covers failures that used to take the whole broker process down, or wedge a subscription with no explanation
/// </summary>
public class SubscriptionHandlerResilienceTests
{

    /// <summary>
    /// Regression guard for issue #36: a 4xx from a subscriber escaped <c>DispatchAsync</c> as an <see cref="HttpRequestException"/>,
    /// reached the hosted service and stopped the host, taking every other subscription with it.
    /// </summary>
    /// <remarks>
    /// The subscriber recovers on the second attempt on purpose. A permanently-failing 4xx cannot be asserted here yet: the retry policy is
    /// currently bypassed (issue #131), so <c>maxAttempts</c> has no effect and an always-failing subscriber recurses without bound.
    /// Add that case once #131 lands.
    /// </remarks>
    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Gone)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task Dispatch_Should_Not_Let_A_Subscriber_Error_Escape_The_Handler(HttpStatusCode statusCode)
    {
        // arrange
        await using var context = await BrokerTestContext.CreateAsync(StubHttpMessageHandler.FailsThenSucceeds(1, statusCode),
            configureSubscription: s => s.Spec.Subscriber.RetryPolicy = new() { MaxAttempts = 2, BackoffDuration = RetryBackoffDuration.Constant(Duration.FromSeconds(0)) });

        // act - the assertion is that this does not throw
        var exception = await Record.ExceptionAsync(() => context.Handler.DispatchRecordAsync(BrokerTestContext.CreateRecord(41)));

        // assert
        Assert.Null(exception);
        Assert.Equal(2, context.Http.AttemptCount);
        Assert.Equal(1, context.Metrics.TotalDeliveryFailures);
    }

    /// <summary>
    /// Regression guard for issue #36: while a subscriber is failing, the subscription must be reported unreachable with a reason rather than failing silently.
    /// </summary>
    [Fact]
    public async Task Dispatch_Should_Report_The_Subscriber_As_Unreachable_While_It_Is_Failing()
    {
        // arrange
        await using var context = await BrokerTestContext.CreateAsync(StubHttpMessageHandler.FailsThenSucceeds(1, HttpStatusCode.BadRequest),
            configureSubscription: s => s.Spec.Subscriber.RetryPolicy = new() { MaxAttempts = 2, BackoffDuration = RetryBackoffDuration.Constant(Duration.FromSeconds(0)) });

        // act
        await context.Handler.DispatchRecordAsync(BrokerTestContext.CreateRecord(41));

        // assert
        Assert.True(context.Patches.Touched("/status/subscriber/state"), context.Patches.Describe());
        Assert.Contains("unreachable", context.Patches.ValuesWrittenTo("/status/subscriber/state").Select(v => v.ToString()));
        Assert.Contains(nameof(HttpStatusCode.BadRequest), string.Join(' ', context.Patches.ValuesWrittenTo("/status/subscriber/reason").Select(v => v.ToString())));
        // the subscriber recovers on the second attempt, so the handler must walk it back to reachable rather than leaving it stuck
        Assert.Contains("reachable", context.Patches.ValuesWrittenTo("/status/subscriber/state").Select(v => v.ToString()));
    }

    /// <summary>
    /// Regression guard for issue #113: when the event store handed back a null stream, the broker blew up deep inside Rx with
    /// <c>ArgumentNullException: Value cannot be null. (Parameter 'source')</c>, which said nothing about which subscription was affected.
    /// Pull request #126 hardened the contract, so the failure must now be self-explaining and recorded on the resource.
    /// </summary>
    [Fact]
    public async Task Initialization_Should_Fault_With_A_Self_Explaining_Error_When_The_Observed_Stream_Is_Null()
    {
        // arrange
        await using var context = await BrokerTestContext.CreateAsync(configureEventStore: store => store.ObserveReturnsNull = true, waitForStreamInitialization: false);

        // act
        await WaitUntilAsync(() => context.Patches.Touched("/status/stream/fault"), "the subscription to be faulted", context);

        // assert
        var fault = context.Patches.Operations.Single(o => o.Path.ToString() == "/status/stream/fault").Value!.ToString();
        Assert.DoesNotContain(nameof(ArgumentNullException), fault);
        Assert.Contains("test-subscription", fault);
    }

    /// <summary>
    /// Regression guard for issue #113: a faulted subscription must never keep dispatching. The guard in <c>OnCloudEventAsync</c> is what stops it.
    /// </summary>
    [Fact]
    public async Task Live_Events_Should_Be_Dropped_Once_The_Stream_Has_Faulted()
    {
        // arrange
        await using var context = await BrokerTestContext.CreateAsync(streamLength: 1);
        context.Subscription.Status!.Stream!.Fault = new(new("https://cloud-streams.io/tests/fault"), "Test fault", 500);

        // act
        await context.Handler.HandleCloudEventAsync(BrokerTestContext.CreateRecord(41));

        // assert
        Assert.Equal(0, context.Http.AttemptCount);
        Assert.Equal(0, context.Patches.Count);
    }

    static async Task WaitUntilAsync(Func<bool> condition, string description, BrokerTestContext context, TimeSpan? timeout = null)
    {
        var effectiveTimeout = timeout ?? TimeSpan.FromSeconds(5);
        var deadline = DateTimeOffset.UtcNow + effectiveTimeout;
        while (!condition())
        {
            if (DateTimeOffset.UtcNow >= deadline) throw new TimeoutException($"Timed out after {effectiveTimeout} waiting for {description}. Recorded operations:{Environment.NewLine}{context.Patches.Describe()}");
            await Task.Delay(10).ConfigureAwait(false);
        }
    }

}
