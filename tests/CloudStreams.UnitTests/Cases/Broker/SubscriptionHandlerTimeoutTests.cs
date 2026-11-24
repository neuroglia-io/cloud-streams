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
/// Regression guards for issue #132: a subscriber timeout must be treated as a delivery failure, never swallowed as if it were a shutdown
/// </summary>
/// <remarks>
/// <see cref="HttpClient"/> raises a timeout as <see cref="TaskCanceledException"/>, not <see cref="HttpRequestException"/>. It used to slip past the retry
/// path and be absorbed by the cancellation filters that exist to swallow genuine shutdown, skipping the event without a trace.
/// </remarks>
public class SubscriptionHandlerTimeoutTests
{

    static readonly HttpClientRetryPolicy ImmediateRetries = new() { MaxAttempts = 2, BackoffDuration = RetryBackoffDuration.Constant(Duration.FromSeconds(0)) };

    /// <summary>
    /// Live path. A timeout must be retried and, once the policy is exhausted, fault the subscription rather than let the next event step <c>ackedOffset</c> past it.
    /// </summary>
    [Fact]
    public async Task Live_Dispatch_Timeout_Should_Be_Retried_And_Never_Skip_The_Event()
    {
        // arrange
        await using var context = await BrokerTestContext.CreateAsync(StubHttpMessageHandler.AlwaysTimesOut(), configureSubscription: s => s.Spec.Subscriber.RetryPolicy = ImmediateRetries);

        // act
        var exception = await Record.ExceptionAsync(() => context.Handler.HandleCloudEventAsync(BrokerTestContext.CreateRecord(41)));

        // assert
        Assert.Null(exception);
        Assert.Equal(3, context.Http.AttemptCount);
        Assert.Equal(3, context.Metrics.TotalDeliveryFailures);
        Assert.Contains("timeout", string.Join(' ', context.Patches.ValuesWrittenTo("/status/subscriber/reason").Select(v => v.ToString())));
        Assert.True(context.Patches.Touched("/status/stream/fault"), context.Patches.Describe());
        Assert.False(context.Patches.Touched("/status/stream/ackedOffset"));
    }

    /// <summary>
    /// A timeout is routed through the retry path like any other delivery failure, so a subscriber that recovers gets the event.
    /// </summary>
    [Fact]
    public async Task Dispatch_Timeout_Should_Enter_The_Retry_Path()
    {
        // arrange
        var http = new StubHttpMessageHandler((_, attempt, _) => attempt == 0
            ? throw new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout of 100 seconds elapsing.", new TimeoutException())
            : Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        await using var context = await BrokerTestContext.CreateAsync(http, configureSubscription: s => s.Spec.Subscriber.RetryPolicy = ImmediateRetries);

        // act
        var exception = await Record.ExceptionAsync(() => context.Handler.DispatchRecordAsync(BrokerTestContext.CreateRecord(41)));

        // assert
        Assert.Null(exception);
        Assert.Equal(2, context.Http.AttemptCount);
        Assert.Equal(1, context.Metrics.TotalDeliveryFailures);
        Assert.Contains(42ul, context.Patches.OffsetsWrittenTo());
    }

    /// <summary>
    /// The dispatch timeout is configurable per subscription, and a subscriber slower than it is retried.
    /// </summary>
    [Fact]
    public async Task Configured_Subscriber_Timeout_Should_Be_Enforced()
    {
        // arrange
        var http = new StubHttpMessageHandler(async (_, attempt, cancellationToken) =>
        {
            if (attempt == 0) await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        await using var context = await BrokerTestContext.CreateAsync(http, configureSubscription: s =>
        {
            s.Spec.Subscriber.Timeout = Duration.FromMilliseconds(100);
            s.Spec.Subscriber.RetryPolicy = ImmediateRetries;
        });

        // act
        await context.Handler.DispatchRecordAsync(BrokerTestContext.CreateRecord(41)).WaitAsync(TimeSpan.FromSeconds(5));

        // assert
        Assert.Equal(2, context.Http.AttemptCount);
        Assert.Contains(42ul, context.Patches.OffsetsWrittenTo());
    }

}
