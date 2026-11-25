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
/// Covers the dead-lettering of the cloud events a subscription fails to deliver (see #207)
/// </summary>
public class SubscriptionHandlerDeadLetterTests
{

    static readonly Uri SinkUri = new("http://localhost:9999/dead-letters");

    static HttpClientRetryPolicy Policy(int maxAttempts, params int[] statusCodes) => new()
    {
        MaxAttempts = maxAttempts,
        BackoffDuration = RetryBackoffDuration.Constant(Duration.FromTimeSpan(TimeSpan.Zero)),
        StatusCodes = statusCodes.Length == 0 ? null : [.. statusCodes]
    };

    /// <summary>
    /// Creates a subscriber that refuses the cloud event at the specified offset, and accepts every other request, including those sent to the dead-letter service
    /// </summary>
    static StubHttpMessageHandler RefusesOffset(string offset, HttpStatusCode status = HttpStatusCode.InternalServerError) => new((request, _, _) =>
        Task.FromResult(new HttpResponseMessage(request.RequestUri != SinkUri && request.Headers.TryGetValues(CloudStreamsHeaders.Offset, out var values) && values.Single() == offset ? status : HttpStatusCode.OK)));

    [Fact]
    public async Task An_Event_Whose_Retries_Are_Exhausted_Should_Be_Dead_Lettered_And_Its_Offset_Committed()
    {
        // arrange
        await using var context = await BrokerTestContext.CreateAsync(StubHttpMessageHandler.AlwaysResponds(HttpStatusCode.InternalServerError), configureSubscription: s =>
        {
            s.Spec.Subscriber.RetryPolicy = Policy(1);
            s.Spec.DeadLetter = new();
        });

        // act
        var handled = context.Handler.DispatchRecordAsync(BrokerTestContext.CreateRecord(41));
        await handled.WaitAsync(TimeSpan.FromSeconds(5));

        // assert - the subscription carries on with the next event, rather than being faulted
        Assert.Equal(2, context.Http.AttemptCount);
        var deadLetter = Assert.Single(context.DeadLetters.All);
        Assert.Equal(41ul, deadLetter.EventOffset);
        Assert.Equal("com.cloud-streams.test.v1", deadLetter.Event.Type);
        Assert.Contains("after 2 attempt(s)", deadLetter.Reason.Detail);
        Assert.False(context.Patches.Touched("/status/stream/fault"));
        Assert.Contains(42ul, context.Patches.OffsetsWrittenTo());
        Assert.Equal(1, context.Metrics.TotalDeadLetters);
    }

    [Fact]
    public async Task An_Event_Refused_With_A_Status_Code_That_Is_Not_Retried_Should_Be_Dead_Lettered_At_Once()
    {
        // arrange
        await using var context = await BrokerTestContext.CreateAsync(StubHttpMessageHandler.AlwaysResponds(HttpStatusCode.BadRequest), configureSubscription: s =>
        {
            s.Spec.Subscriber.RetryPolicy = Policy(5, 503);
            s.Spec.DeadLetter = new();
        });

        // act
        await context.Handler.DispatchRecordAsync(BrokerTestContext.CreateRecord(41)).WaitAsync(TimeSpan.FromSeconds(5));

        // assert
        Assert.Equal(1, context.Http.AttemptCount);
        Assert.Contains("not configured to retry", Assert.Single(context.DeadLetters.All).Reason.Detail);
    }

    [Fact]
    public async Task Catching_Up_Should_Carry_On_After_A_Dead_Lettered_Event()
    {
        // arrange
        await using var context = await BrokerTestContext.CreateAsync(RefusesOffset("1"), streamLength: 3, waitForStreamInitialization: false, configureSubscription: s =>
        {
            s.Spec.Stream = new() { Offset = 0 };
            s.Spec.Subscriber.RetryPolicy = Policy(0);
            s.Spec.DeadLetter = new();
        });
        // the catch-up started by the initialization has settled once every record has been attempted
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (context.Http.AttemptCount < 3 && DateTime.UtcNow < deadline) await Task.Delay(20);

        // assert
        Assert.Equal(["0", "1", "2"], context.Http.Attempts.Select(a => a.Headers[CloudStreamsHeaders.Offset]));
        Assert.Equal(1ul, Assert.Single(context.DeadLetters.All).EventOffset);
        Assert.False(context.Patches.Touched("/status/stream/fault"));
    }

    [Fact]
    public async Task An_Event_Should_Be_Sent_To_The_Dead_Letter_Service_If_The_Policy_Defines_One()
    {
        // arrange
        await using var context = await BrokerTestContext.CreateAsync(RefusesOffset("41", HttpStatusCode.BadRequest), configureSubscription: s =>
        {
            s.Spec.Subscriber.RetryPolicy = Policy(0);
            s.Spec.DeadLetter = new() { Uri = SinkUri };
        });

        // act
        await context.Handler.DispatchRecordAsync(BrokerTestContext.CreateRecord(41)).WaitAsync(TimeSpan.FromSeconds(5));

        // assert
        var sent = Assert.Single(context.Http.Attempts, a => a.RequestUri == SinkUri);
        Assert.Equal("41", sent.Headers[CloudStreamsHeaders.Offset]);
        Assert.Contains("BadRequest", Uri.UnescapeDataString(sent.Headers[CloudStreamsHeaders.DeadLetterReason]));
        Assert.Empty(context.DeadLetters.All);
        Assert.False(context.Patches.Touched("/status/stream/fault"));
        Assert.Equal(1, context.Metrics.TotalDeadLetters);
    }

    [Fact]
    public async Task An_Event_That_Cannot_Be_Dead_Lettered_Should_Fault_The_Subscription()
    {
        // arrange
        await using var context = await BrokerTestContext.CreateAsync(StubHttpMessageHandler.AlwaysResponds(HttpStatusCode.BadRequest), configureSubscription: s =>
        {
            s.Spec.Subscriber.RetryPolicy = Policy(0);
            s.Spec.DeadLetter = new();
        });
        context.DeadLetters.Failure = new InvalidOperationException("the event store is unavailable");

        // act
        await context.Handler.DispatchRecordAsync(BrokerTestContext.CreateRecord(41)).WaitAsync(TimeSpan.FromSeconds(5));

        // assert - the event is neither delivered nor set aside: it must not be skipped
        var fault = context.Patches.Operations.Single(o => o.Path.ToString() == "/status/stream/fault").Value!.ToString();
        Assert.Contains("could not be dead-lettered either", fault);
        Assert.Contains("the event store is unavailable", fault);
        Assert.False(context.Patches.Touched("/status/stream/ackedOffset"));
        Assert.Equal(0, context.Metrics.TotalDeadLetters);
    }

    [Fact]
    public async Task Without_A_Dead_Letter_Policy_The_Subscription_Should_Still_Be_Faulted()
    {
        // arrange
        await using var context = await BrokerTestContext.CreateAsync(StubHttpMessageHandler.AlwaysResponds(HttpStatusCode.BadRequest), configureSubscription: s => s.Spec.Subscriber.RetryPolicy = Policy(0));

        // act
        await context.Handler.DispatchRecordAsync(BrokerTestContext.CreateRecord(41)).WaitAsync(TimeSpan.FromSeconds(5));

        // assert
        Assert.True(context.Patches.Touched("/status/stream/fault"));
        Assert.Empty(context.DeadLetters.All);
    }

}
