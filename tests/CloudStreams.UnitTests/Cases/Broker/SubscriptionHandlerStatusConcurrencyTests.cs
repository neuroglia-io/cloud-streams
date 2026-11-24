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

using Json.Patch;
using Neuroglia.Data.PatchModel.Services;

namespace CloudStreams.UnitTests.Cases.Broker;

/// <summary>
/// Covers the optimistic concurrency of the status patches applied by the broker (see #161)
/// </summary>
public class SubscriptionHandlerStatusConcurrencyTests
{

    static ProblemDetailsException Conflict() => new(new(new Uri("https://cloud-streams.io/problems/patch-conflict"), "Conflict", (int)HttpStatusCode.Conflict));

    static Subscription WithVersion(Subscription subscription, string version)
    {
        var copy = subscription.Clone()!;
        copy.Metadata.ResourceVersion = version;
        return copy;
    }

    /// <summary>
    /// Replaces the status patches of the repository with the specified behaviour, still recording every patch
    /// </summary>
    static void OnPatch(BrokerTestContext context, Func<int, Patch, Task<IResource>> handle)
    {
        var calls = 0;
        context.Repository.PatchSubResourceAsync(Arg.Any<Patch>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var patch = call.Arg<Patch>()!;
                context.Patches.Record(patch, call.ArgAt<string?>(7));
                return handle(Interlocked.Increment(ref calls), patch);
            });
    }

    static void OnGet(BrokerTestContext context, Subscription subscription) => context.Repository.GetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
        .Returns(Task.FromResult<IResource?>(subscription));

    [Fact]
    public async Task A_Status_Patch_Should_Expect_The_Version_Left_By_The_Previous_One()
    {
        // arrange
        await using var context = await BrokerTestContext.CreateAsync(configureSubscription: s => s.Metadata.ResourceVersion = "v1");
        OnPatch(context, (call, _) => Task.FromResult<IResource>(WithVersion(context.Subscription, $"v{call + 1}")));

        // act
        await context.Handler.CommitAsync(5);
        await context.Handler.CommitAsync(6);

        // assert
        Assert.Equal(["v1", "v2"], context.Patches.ResourceVersions);
    }

    [Fact]
    public async Task A_Status_Patch_Should_Expect_The_Version_Reported_By_The_Resource_Watch()
    {
        // arrange
        await using var context = await BrokerTestContext.CreateAsync(configureSubscription: s => s.Metadata.ResourceVersion = "v1");
        context.PublishSubscriptionUpdate(WithVersion(context.Subscription, "v9"));

        // act
        await context.Handler.CommitAsync(5);

        // assert
        Assert.Equal(["v9"], context.Patches.ResourceVersions);
    }

    [Fact]
    public async Task A_Conflicting_Status_Patch_Should_Be_Recomputed_Against_The_Current_Status()
    {
        // arrange: the handler has not seen that the subscriber deferred an acknowledgement, which another writer recorded in the meantime
        await using var context = await BrokerTestContext.CreateAsync(configureSubscription: s =>
        {
            s.Metadata.ResourceVersion = "v1";
            s.Status!.Subscriber = null;
        });
        var current = WithVersion(context.Subscription, "v2");
        current.Status!.Subscriber = new() { State = SubscriberState.Reachable, PendingAck = new() { Offset = 3, Token = "token", ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(1) } };
        OnGet(context, current);
        OnPatch(context, (call, _) => call == 1 ? Task.FromException<IResource>(Conflict()) : Task.FromResult<IResource>(current));

        // act
        await context.Handler.ReportSubscriberStateAsync(SubscriberState.Unreachable, "down");

        // assert: the retry expects the current version, and keeps what the other writer recorded
        Assert.Equal(["v1", "v2"], context.Patches.ResourceVersions);
        var patched = await new JsonPatchHandler().ApplyPatchAsync(context.Patches.Patches[1].Document, current.Clone());
        Assert.Equal(SubscriberState.Unreachable, patched!.Status!.Subscriber!.State);
        Assert.Equal("down", patched.Status.Subscriber.Reason);
        Assert.NotNull(patched.Status.Subscriber.PendingAck);
        Assert.Equal(3ul, patched.Status.Subscriber.PendingAck.Offset);
    }

    [Fact]
    public async Task A_Status_Patch_That_Keeps_Conflicting_Should_Eventually_Fail()
    {
        // arrange
        await using var context = await BrokerTestContext.CreateAsync(configureSubscription: s => s.Metadata.ResourceVersion = "v1");
        OnGet(context, WithVersion(context.Subscription, "v2"));
        OnPatch(context, (_, _) => Task.FromException<IResource>(Conflict()));

        // act
        var exception = await Assert.ThrowsAsync<ProblemDetailsException>(() => context.Handler.CommitAsync(5));

        // assert
        Assert.Equal((int)HttpStatusCode.Conflict, exception.Problem.Status);
        Assert.Equal(5, context.Patches.Count);
    }

    [Fact]
    public async Task A_Status_Patch_That_Changes_Nothing_Should_Not_Be_Sent()
    {
        // arrange
        await using var context = await BrokerTestContext.CreateAsync(configureSubscription: s => s.Status!.Stream!.AckedOffset = 5);

        // act
        await context.Handler.CommitAsync(5);

        // assert
        Assert.Equal(0, context.Patches.Count);
    }

}
