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
/// Covers the deferred acknowledgement of cloud events requested by issue #133: a subscriber answers '202 Accepted', and calls back later
/// </summary>
public class SubscriptionHandlerAcknowledgementTests
{

    /// <summary>
    /// Simulates what the gateway does when the subscriber calls back, as seen by the broker through the resource watch: first the pending acknowledgement
    /// the broker recorded, then its resolution
    /// </summary>
    static void SimulateCallback(BrokerTestContext context, ulong? ackedOffset)
    {
        var pendingAck = new PendingAcknowledgement()
        {
            Offset = context.Patches.ValuesWrittenTo("/status/subscriber/pendingAck").Last()["offset"]?.GetValue<ulong>() ?? 0, // the serializer omits default values
            Token = context.Patches.ValuesWrittenTo("/status/subscriber/pendingAck").Last()["token"]!.GetValue<string>(),
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5)
        };
        var pending = context.Handler.CurrentSubscription.Clone()!;
        pending.Status!.Subscriber!.PendingAck = pendingAck;
        context.PublishSubscriptionUpdate(pending);
        var resolved = pending.Clone()!;
        resolved.Status!.Subscriber!.PendingAck = null;
        resolved.Status.Subscriber.LastAcknowledgedAt = DateTimeOffset.UtcNow;
        resolved.Status.Stream!.AckedOffset = ackedOffset ?? resolved.Status.Stream.AckedOffset;
        context.PublishSubscriptionUpdate(resolved);
    }

    [Fact]
    public async Task Every_Delivery_Should_Advertise_How_To_Defer_Its_Acknowledgement()
    {
        // arrange
        await using var context = await BrokerTestContext.CreateAsync(configureBroker: b => b.Spec.Dispatch = new() { Ack = new() { CallbackUri = new("https://cloud-streams.example.com/") } });

        // act
        await context.Handler.DispatchRecordAsync(BrokerTestContext.CreateRecord(41));

        // assert
        var headers = context.Http.Attempts.Single().Headers;
        Assert.Equal("test-subscription", headers[CloudStreamsHeaders.Subscription]);
        Assert.Equal("41", headers[CloudStreamsHeaders.Offset]);
        Assert.True(headers[CloudStreamsHeaders.AckToken].Length >= 32);
        Assert.Equal("https://cloud-streams.example.com/api/resources/v1/subscriptions/test-subscription/ack", headers[CloudStreamsHeaders.AckUri]);
    }

    [Fact]
    public async Task Accepted_Should_Suspend_Delivery_Without_Acknowledging_The_Event()
    {
        // arrange
        await using var context = await BrokerTestContext.CreateAsync(StubHttpMessageHandler.AlwaysResponds(HttpStatusCode.Accepted));

        // act
        await context.Handler.DispatchRecordAsync(BrokerTestContext.CreateRecord(41));
        await context.Handler.HandleCloudEventAsync(BrokerTestContext.CreateRecord(42));

        // assert - the token recorded is the one the subscriber received, the offset is not committed, and live events are dropped
        var pendingAck = context.Patches.ValuesWrittenTo("/status/subscriber/pendingAck").Single();
        Assert.Equal(41ul, pendingAck["offset"]!.GetValue<ulong>());
        // the subscriber gets the token, while the status only records its hash (see #149)
        Assert.Equal(AcknowledgementTokens.Hash(context.Http.Attempts[0].Headers[CloudStreamsHeaders.AckToken]), pendingAck["token"]!.GetValue<string>());
        Assert.False(context.Patches.Touched("/status/stream/ackedOffset"));
        Assert.True(context.Handler.IsOutOfSync);
        Assert.Equal(1, context.Http.AttemptCount);
        Assert.Equal(1, context.Metrics.PendingAcknowledgements);
    }

    [Fact]
    public async Task Acknowledgement_Should_Resume_Delivery_After_The_Acknowledged_Event()
    {
        // arrange
        await using var context = await BrokerTestContext.CreateAsync(StubHttpMessageHandler.Responds(HttpStatusCode.Accepted, HttpStatusCode.OK), streamLength: 3);
        await context.Handler.DispatchRecordAsync(context.EventStore.Records[0]);

        // act
        SimulateCallback(context, ackedOffset: 1);

        // assert - records 1 and 2, which were published in the meantime, are delivered next
        await WaitUntilAsync(() => !context.Handler.IsOutOfSync && context.Http.AttemptCount >= 3, "delivery to resume", context);
        Assert.Equal(3, context.Http.AttemptCount);
        Assert.Equal(["1", "2"], context.Http.Attempts.Skip(1).Select(a => a.Headers[CloudStreamsHeaders.Offset]));
        Assert.Equal(0, context.Metrics.PendingAcknowledgements);
    }

    [Fact]
    public async Task Negative_Acknowledgement_Should_Redeliver_The_Event()
    {
        // arrange
        await using var context = await BrokerTestContext.CreateAsync(StubHttpMessageHandler.Responds(HttpStatusCode.Accepted, HttpStatusCode.OK), streamLength: 2);
        await context.Handler.DispatchRecordAsync(context.EventStore.Records[0]);

        // act
        SimulateCallback(context, ackedOffset: null);

        // assert
        await WaitUntilAsync(() => !context.Handler.IsOutOfSync && context.Http.AttemptCount >= 3, "delivery to resume", context);
        Assert.Equal(["0", "0", "1"], context.Http.Attempts.Select(a => a.Headers[CloudStreamsHeaders.Offset]));
    }

    [Fact]
    public async Task Expired_Acknowledgement_From_A_Subscriber_That_Never_Called_Back_Should_Fault_The_Subscription()
    {
        // arrange - the subscriber most likely misuses '202 Accepted' to mean "done"
        await using var context = await BrokerTestContext.CreateAsync(StubHttpMessageHandler.AlwaysResponds(HttpStatusCode.Accepted),
            configureSubscription: s => s.Spec.Subscriber.Ack = new() { Timeout = Duration.FromMilliseconds(150) });

        // act
        await context.Handler.DispatchRecordAsync(BrokerTestContext.CreateRecord(41));

        // assert
        await WaitUntilAsync(() => context.Patches.Touched("/status/stream/fault"), "the subscription to be faulted", context);
        Assert.Contains("200 OK", context.Patches.ValuesWrittenTo("/status/stream/fault").Single().ToJsonString());
        Assert.Contains(context.Patches.Operations, o => o.Op == Json.Patch.OperationType.Test && o.Path.ToString() == "/status/subscriber/pendingAck/token");
        Assert.Contains(context.Patches.Operations, o => o.Op == Json.Patch.OperationType.Remove && o.Path.ToString() == "/status/subscriber/pendingAck");
        Assert.Equal(1, context.Metrics.TotalAcknowledgementTimeouts);
        Assert.Equal(0, context.Metrics.PendingAcknowledgements);
        Assert.Equal(1, context.Http.AttemptCount);
    }

    [Fact]
    public async Task Expired_Acknowledgement_Should_Otherwise_Redeliver_The_Event()
    {
        // arrange
        await using var context = await BrokerTestContext.CreateAsync(StubHttpMessageHandler.Responds(HttpStatusCode.Accepted, HttpStatusCode.OK), streamLength: 1,
            configureSubscription: s =>
            {
                s.Spec.Subscriber.Ack = new() { Timeout = Duration.FromMilliseconds(150) };
                s.Status!.Subscriber!.LastAcknowledgedAt = DateTimeOffset.UtcNow.AddDays(-1);
            });

        // act
        await context.Handler.DispatchRecordAsync(context.EventStore.Records[0]);

        // assert
        await WaitUntilAsync(() => context.Http.AttemptCount >= 2 && !context.Handler.IsOutOfSync, "the event to be redelivered", context);
        Assert.Equal(["0", "0"], context.Http.Attempts.Select(a => a.Headers[CloudStreamsHeaders.Offset]));
        Assert.Contains(1ul, context.Patches.OffsetsWrittenTo());
        Assert.False(context.Patches.Touched("/status/stream/fault"));
        Assert.Equal(1, context.Metrics.TotalAcknowledgementTimeouts);
    }

    [Fact]
    public async Task Accepted_Should_Be_A_Plain_Success_For_A_Subscription_That_Does_Not_Track_An_Offset()
    {
        // arrange
        await using var context = await BrokerTestContext.CreateAsync(StubHttpMessageHandler.AlwaysResponds(HttpStatusCode.Accepted), configureSubscription: s => s.Spec.Stream = null);

        // act
        await context.Handler.DispatchRecordAsync(BrokerTestContext.CreateRecord(41));

        // assert
        Assert.False(context.Http.Attempts.Single().Headers.ContainsKey(CloudStreamsHeaders.AckToken));
        Assert.False(context.Patches.Touched("/status/subscriber/pendingAck"));
        Assert.False(context.Handler.IsOutOfSync);
    }

    [Fact]
    public async Task A_Pending_Acknowledgement_Should_Survive_A_Broker_Restart()
    {
        // arrange
        await using var context = await BrokerTestContext.CreateAsync(streamLength: 2,
            configureSubscription: s => s.Status!.Subscriber!.PendingAck = new() { Offset = 0, Token = "pending-token", ExpiresAt = DateTimeOffset.UtcNow.AddHours(1) });

        // act
        await context.Handler.HandleCloudEventAsync(BrokerTestContext.CreateRecord(2));

        // assert
        Assert.True(context.Handler.IsOutOfSync);
        Assert.Equal(0, context.Http.AttemptCount);
        Assert.Equal(1, context.Metrics.PendingAcknowledgements);
    }

    static async Task WaitUntilAsync(Func<bool> condition, string description, BrokerTestContext context, TimeSpan? timeout = null)
    {
        var effectiveTimeout = timeout ?? TimeSpan.FromSeconds(5);
        var deadline = DateTimeOffset.UtcNow + effectiveTimeout;
        while (!condition())
        {
            if (DateTimeOffset.UtcNow >= deadline) throw new TimeoutException($"Timed out after {effectiveTimeout} waiting for {description}. Attempts: {context.Http.AttemptCount}. Recorded operations:{Environment.NewLine}{context.Patches.Describe()}");
            await Task.Delay(10).ConfigureAwait(false);
        }
    }

}
