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
/// Covers the resume lever. A bounded default retry policy is only safe if a subscription that spends its attempts can be
/// brought back: faulting is what makes the failure visible, and re-initialization is what makes it recoverable.
/// </summary>
public class SubscriptionHandlerRecoveryTests
{

    [Fact]
    public async Task Clearing_The_Fault_Should_Reinitialize_The_Stream()
    {
        //arrange
        //a subscription that tracks no offset has no spec field to edit, so clearing status.stream.fault is the only resume
        //lever it has. Nothing else restarts the stream, and the initialization guard returns early for as long as the fault
        //stands, so a fault-cleared update that reaches nothing leaves the subscription deaf for the rest of the process
        await using var context = await SubscriptionHandlerTestContext.CreateAsync(initializeStream: true, offset: null);
        await context.WaitForStreamInitializationsAsync(1);
        var initializations = context.Handler.StreamInitializationCount;
        var faulted = context.Repository.Current.Clone()!;
        faulted.Status ??= new();
        faulted.Status.Stream ??= new();
        faulted.Status.Stream.Fault = new ProblemDetails(new("https://cloud-streams.io/errors/dispatch"), "Delivery failed", 500);
        context.PushSubscriptionUpdate(faulted);

        //act
        var resumed = faulted.Clone()!;
        resumed.Status!.Stream!.Fault = null;
        context.PushSubscriptionUpdate(resumed);

        //assert
        await context.WaitForStreamInitializationsAsync(initializations + 1);
    }

    [Fact]
    public async Task Editing_The_Offset_Should_Resume_A_Faulted_Subscription()
    {
        //arrange
        //editing spec.stream.offset is the documented way to resume. Clearing the fault and returning is only half of it: the
        //edited offset has already been consumed by DistinctUntilChanged, so an operator who sees nothing happen has no
        //second lever to pull
        using var clock = RecordingSystemClock.Install();
        await using var context = await SubscriptionHandlerTestContext.CreateAsync(SubscriptionFactory.RetryPolicy(maxAttempts: 1), initializeStream: true);
        context.Http.Respond(HttpStatusCode.ServiceUnavailable);
        await context.Handler.DispatchPublicAsync(SubscriptionFactory.CloudEvent(), 0);
        context.Repository.LastValueAt("/status/stream/fault").Should().NotBeNull("the subscription has to be faulted before there is anything to resume");
        var initializations = context.Handler.StreamInitializationCount;

        //act
        var resumed = context.Repository.Current.Clone()!;
        resumed.Spec.Stream!.Offset = 5;
        context.PushSubscriptionUpdate(resumed);

        //assert
        await context.WaitForStreamInitializationsAsync(initializations + 1);
        context.Repository.LastValueAt("/status/stream/fault").Should().BeNull("resuming clears the fault");
    }

    [Fact]
    public async Task A_Healthy_Status_Update_Should_Not_Reinitialize_The_Stream()
    {
        //arrange
        //the fault observable emits whatever the field holds, not only faults, so the first status update of a subscription
        //that was never faulted also carries a null. Acting on that would restart every subscription on its first offset commit
        await using var context = await SubscriptionHandlerTestContext.CreateAsync(initializeStream: true);
        await context.WaitForStreamInitializationsAsync(1);
        var initializations = context.Handler.StreamInitializationCount;
        var updated = context.Repository.Current.Clone()!;
        updated.Status ??= new();
        updated.Status.Stream ??= new();
        updated.Status.Stream.AckedOffset = 1;

        //act
        context.PushSubscriptionUpdate(updated);
        await Task.Delay(250);

        //assert
        context.Handler.StreamInitializationCount.Should().Be(initializations, "a subscription that was never faulted has nothing to resume from");
    }

    [Fact]
    public async Task Dispatch_Should_Report_The_Subscriber_As_Unreachable_Without_Any_Retry()
    {
        //arrange
        //'maxAttempts: 1' is a legitimate deliver-once configuration, and it builds a policy with zero retries - so the retry
        //callback that reports the subscriber never fires. The badge would keep reading 'unknown', or a stale 'reachable'
        using var clock = RecordingSystemClock.Install();
        await using var context = await SubscriptionHandlerTestContext.CreateAsync(SubscriptionFactory.RetryPolicy(maxAttempts: 1));
        context.Http.Respond(HttpStatusCode.ServiceUnavailable);

        //act
        await context.Handler.DispatchPublicAsync(SubscriptionFactory.CloudEvent(), 0);

        //assert
        context.Http.RequestCount.Should().Be(1, "maxAttempts counts the first attempt, so one means no retries");
        context.Repository.LastValueAt("/status/subscriber/state").Should().Be("unreachable");
        context.Repository.LastValueAt("/status/stream/fault").Should().NotBeNull();
    }

    [Fact]
    public async Task Dispatch_Should_Bound_The_Reason_It_Copies_From_The_Subscriber()
    {
        //arrange
        //the reason is persisted on a cluster-visible resource. A reverse proxy's HTML error page would otherwise be copied
        //onto it verbatim, once per failed attempt
        using var clock = RecordingSystemClock.Install();
        await using var context = await SubscriptionHandlerTestContext.CreateAsync(SubscriptionFactory.RetryPolicy(maxAttempts: 2));
        context.Http.RespondWith(HttpStatusCode.BadGateway, new string('x', 20_000));

        //act
        await context.Handler.DispatchPublicAsync(SubscriptionFactory.CloudEvent(), 0);

        //assert
        context.Repository.LastValueAt("/status/subscriber/reason")!.Length.Should().BeLessThan(1024);
    }

}
