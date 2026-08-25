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
/// Covers the subscriber retry policy, which was inert until <c>DispatchAsync</c> stopped catching its own
/// <see cref="HttpRequestException"/> one frame below the Polly delegate. See
/// <see href="https://github.com/neuroglia-io/cloud-streams/issues/131">#131</see>.
/// </summary>
/// <remarks>
/// <para>
/// <b>maxAttempts means total delivery attempts, including the first.</b> <c>maxAttempts: 3</c> is therefore 3 requests
/// separated by 2 backoff waits. That is why the initial attempt happens <i>inside</i> the policy: dispatching once before
/// handing off would add an extra, un-delayed request per event.
/// </para>
/// <para>
/// Backoff is asserted through <see cref="RecordingSystemClock"/> rather than by measuring elapsed time, so these tests are
/// exact and take milliseconds instead of seconds.
/// </para>
/// </remarks>
public class SubscriptionHandlerRetryTests
{

    [Fact]
    public async Task Dispatch_Should_Honour_MaxAttempts_And_Backoff()
    {
        //arrange
        using var clock = RecordingSystemClock.Install();
        await using var context = await SubscriptionHandlerTestContext.CreateAsync(SubscriptionFactory.RetryPolicy(maxAttempts: 3, period: TimeSpan.FromSeconds(3)));
        context.Http.Respond(HttpStatusCode.ServiceUnavailable);

        //act
        await context.Handler.DispatchPublicAsync(SubscriptionFactory.CloudEvent(), 0);

        //assert
        context.Http.RequestCount.Should().Be(3, "maxAttempts counts the first delivery attempt as well as the retries");
        clock.Delays.Should().Equal(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task Dispatch_Should_Stop_Retrying_Once_The_Subscriber_Recovers()
    {
        //arrange
        using var clock = RecordingSystemClock.Install();
        await using var context = await SubscriptionHandlerTestContext.CreateAsync(SubscriptionFactory.RetryPolicy(maxAttempts: 5, period: TimeSpan.FromSeconds(3)));
        context.Http.Script(HttpStatusCode.ServiceUnavailable, HttpStatusCode.ServiceUnavailable, HttpStatusCode.OK);

        //act
        await context.Handler.DispatchPublicAsync(SubscriptionFactory.CloudEvent(), 0);

        //assert
        context.Http.RequestCount.Should().Be(3);
        clock.Delays.Should().HaveCount(2);
        context.Metrics.TotalPublishedEvents.Should().Be(1);
    }

    [Fact]
    public async Task Dispatch_Should_Count_Real_Attempts_Not_Recursion_Depth()
    {
        //arrange
        //TotalDeliveryFailures is incremented once per recursion level today, so it tracks how deep the loop went rather
        //than how many times the subscriber was actually called. Once the policy drives the retries, the two must agree
        using var clock = RecordingSystemClock.Install();
        await using var context = await SubscriptionHandlerTestContext.CreateAsync(SubscriptionFactory.RetryPolicy(maxAttempts: 3, period: TimeSpan.FromSeconds(3)));
        context.Http.Respond(HttpStatusCode.ServiceUnavailable);

        //act
        await context.Handler.DispatchPublicAsync(SubscriptionFactory.CloudEvent(), 0);

        //assert
        context.Metrics.TotalDeliveryFailures.Should().Be(context.Http.RequestCount);
    }

    [Fact]
    public async Task Dispatch_Should_Apply_The_Backoff_Type_Declared_By_The_Policy()
    {
        //arrange
        using var clock = RecordingSystemClock.Install();
        var retryPolicy = SubscriptionFactory.RetryPolicy(maxAttempts: 4);
        retryPolicy.BackoffDuration = RetryBackoffDuration.Incremental(Duration.FromSeconds(2));
        await using var context = await SubscriptionHandlerTestContext.CreateAsync(retryPolicy);
        context.Http.Respond(HttpStatusCode.ServiceUnavailable);

        //act
        await context.Handler.DispatchPublicAsync(SubscriptionFactory.CloudEvent(), 0);

        //assert
        clock.Delays.Should().Equal(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(6));
    }

    [Fact]
    public async Task Dispatch_Should_Not_Retry_A_StatusCode_The_Policy_Excludes()
    {
        //arrange
        using var clock = RecordingSystemClock.Install();
        await using var context = await SubscriptionHandlerTestContext.CreateAsync(SubscriptionFactory.RetryPolicy(maxAttempts: 3, statusCodes: [503]));
        context.Http.Respond(HttpStatusCode.BadRequest);

        //act
        await context.Handler.DispatchPublicAsync(SubscriptionFactory.CloudEvent(), 0);

        //assert
        context.Http.RequestCount.Should().Be(1);
        clock.Delays.Should().BeEmpty();
    }

    [Fact]
    public async Task Dispatch_Should_Retry_Transport_Failures_Even_When_StatusCodes_Are_Declared()
    {
        //arrange
        //a connection failure produces an HttpRequestException with a null StatusCode, so a policy declaring
        //'statusCodes' would otherwise stop retrying exactly the failures retrying is most likely to resolve
        using var clock = RecordingSystemClock.Install();
        await using var context = await SubscriptionHandlerTestContext.CreateAsync(SubscriptionFactory.RetryPolicy(maxAttempts: 3, statusCodes: [503]));
        context.Http.Throws(() => new HttpRequestException("connection refused"));

        //act
        await context.Handler.DispatchPublicAsync(SubscriptionFactory.CloudEvent(), 0);

        //assert
        context.Http.RequestCount.Should().Be(3);
        clock.Delays.Should().HaveCount(2);
    }

    [Fact]
    public async Task Dispatch_Should_Report_The_Subscriber_As_Unreachable_While_Retrying()
    {
        //arrange
        using var clock = RecordingSystemClock.Install();
        await using var context = await SubscriptionHandlerTestContext.CreateAsync(SubscriptionFactory.RetryPolicy(maxAttempts: 3));
        context.Http.Respond(HttpStatusCode.ServiceUnavailable);

        //act
        await context.Handler.DispatchPublicAsync(SubscriptionFactory.CloudEvent(), 0);

        //assert
        context.Repository.LastValueAt("/status/subscriber/state").Should().Be("unreachable");
    }

    [Fact]
    public async Task Dispatch_Should_Report_An_Open_Circuit_And_Leave_A_Way_To_Resume()
    {
        //arrange
        //BrokenCircuitException is not handled by the policy builder, so it escapes ExecuteAsync and is caught explicitly.
        //The circuit breaker is rebuilt for every event, so an open circuit is not transient state that closes on its own: it
        //means this event's attempts are spent. Reporting only the subscriber state would leave nothing for an operator to
        //clear, and Unreachable alone blocks the live path permanently - the silent freeze this suite exists to prevent
        using var clock = RecordingSystemClock.Install();
        await using var context = await SubscriptionHandlerTestContext.CreateAsync(SubscriptionFactory.RetryPolicy(circuitBreaker: new CircuitBreakerPolicy(3, Duration.FromSeconds(5))));
        context.Http.Respond(HttpStatusCode.ServiceUnavailable);

        //act
        await context.Handler.DispatchPublicAsync(SubscriptionFactory.CloudEvent(), 0);

        //assert
        context.Http.RequestCount.Should().Be(3, "the breaker opens after the configured number of consecutive failures");
        context.Repository.LastValueAt("/status/subscriber/state").Should().Be("unreachable");
        context.Repository.LastValueAt("/status/subscriber/reason").Should().Contain("circuit", "the reason has to distinguish an open circuit from an ordinary failure");
        context.Repository.LastValueAt("/status/stream/fault").Should().NotBeNull("without a fault there is nothing an operator can clear to resume the subscription");
    }

    [Fact]
    public async Task Dispatch_Should_Back_Off_When_MaxAttempts_Is_Not_Set()
    {
        //arrange
        //'retry forever' remains the contract for a policy that deliberately omits maxAttempts. What made #121 an incident was
        //not the absence of a cap, it was that there was no backoff and no breaker between attempts
        using var clock = RecordingSystemClock.Install();
        await using var context = await SubscriptionHandlerTestContext.CreateAsync(SubscriptionFactory.RetryPolicy(period: TimeSpan.FromSeconds(3), circuitBreaker: new CircuitBreakerPolicy(3, Duration.FromSeconds(5))));
        context.Http.Respond(HttpStatusCode.ServiceUnavailable);

        //act
        await context.Handler.DispatchPublicAsync(SubscriptionFactory.CloudEvent(), 0);

        //assert
        context.Http.RequestCount.Should().BeLessThan(context.Http.MaxRequests);
        clock.Delays.Should().AllSatisfy(delay => delay.Should().Be(TimeSpan.FromSeconds(3)));
    }

    [Fact]
    public async Task Dispatch_Should_Apply_The_Bounded_Default_Policy_When_Nothing_Is_Configured()
    {
        //arrange
        //neither the subscription nor the broker declares a policy, which is where every out-of-the-box deployment lands.
        //Before #121's fix that fell through to a bare HttpClientRetryPolicy: unbounded, and chosen by nobody
        using var clock = RecordingSystemClock.Install();
        await using var context = await SubscriptionHandlerTestContext.CreateAsync();
        context.Http.Respond(HttpStatusCode.ServiceUnavailable);

        //act
        await context.Handler.DispatchPublicAsync(SubscriptionFactory.CloudEvent(), 0);

        //assert
        context.Http.RequestCount.Should().Be(HttpClientRetryPolicy.DefaultMaxAttempts);
        clock.Delays.Should().HaveCount(HttpClientRetryPolicy.DefaultMaxAttempts - 1);
    }

    [Fact]
    public async Task Dispatch_Should_Fault_The_Subscription_Once_Its_Attempts_Are_Spent()
    {
        //arrange
        //giving up has to leave something an operator can see and alert on. It also must not acknowledge the offset: the event
        //stays in the store and is replayed when the subscription is resumed, which is what makes a bounded default safe
        using var clock = RecordingSystemClock.Install();
        await using var context = await SubscriptionHandlerTestContext.CreateAsync(SubscriptionFactory.RetryPolicy(maxAttempts: 2));
        context.Http.Respond(HttpStatusCode.ServiceUnavailable);

        //act
        await context.Handler.DispatchPublicAsync(SubscriptionFactory.CloudEvent(), 0);

        //assert
        context.Repository.LastValueAt("/status/stream/fault").Should().NotBeNull();
        context.Repository.LastValueAt("/status/stream/ackedOffset").Should().BeNull("an event that was never delivered must not be acknowledged");
    }

    [Fact]
    public async Task Dispatch_Should_Fault_A_Subscription_That_Does_Not_Track_An_Offset()
    {
        //arrange
        //OnSubscriptionErrorAsync used to return early when spec.stream was null, so these subscriptions exhausted their
        //attempts and went deaf with no badge, no fault and nothing logged - the silence behind #28, #105 and #132
        using var clock = RecordingSystemClock.Install();
        await using var context = await SubscriptionHandlerTestContext.CreateAsync(SubscriptionFactory.RetryPolicy(maxAttempts: 2), offset: null);
        context.Http.Respond(HttpStatusCode.ServiceUnavailable);

        //act
        await context.Handler.DispatchPublicAsync(SubscriptionFactory.CloudEvent(), 0);

        //assert
        context.Repository.LastValueAt("/status/stream/fault").Should().NotBeNull();
    }

}
