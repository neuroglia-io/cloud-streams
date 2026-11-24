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

using CloudStreams.Core.Application.Commands.Subscriptions;
using Json.Patch;

namespace CloudStreams.UnitTests.Cases.Core;

/// <summary>
/// Covers the acknowledgement endpoints a subscriber calls back after deferring an acknowledgement (issue #133)
/// </summary>
public class AcknowledgeSubscriptionEventCommandTests
{

    const string Token = "the-pending-token";

    static (AcknowledgeSubscriptionEventCommandHandler Handler, StatusPatchRecorder Patches) Create(Subscription? subscription)
    {
        var patches = new StatusPatchRecorder();
        var repository = Substitute.For<IResourceRepository>();
        repository.GetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IResource?>(subscription));
        repository.PatchSubResourceAsync(Arg.Any<Patch>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                patches.Record(call.Arg<Patch>()!);
                return Task.FromResult<IResource>(subscription!);
            });
        return (new AcknowledgeSubscriptionEventCommandHandler(repository), patches);
    }

    static Subscription CreateSubscription(DateTimeOffset? expiresAt = null) => new()
    {
        Metadata = new() { Name = "test-subscription", Generation = 1 },
        Spec = new() { Subscriber = new() { Uri = new("http://localhost:8888/events") }, Stream = new() },
        Status = new()
        {
            ObservedGeneration = 1,
            Stream = new() { AckedOffset = 41 },
            Subscriber = new() { State = SubscriberState.Reachable, PendingAck = new() { Offset = 41, Token = AcknowledgementTokens.Hash(Token), ExpiresAt = expiresAt ?? DateTimeOffset.UtcNow.AddMinutes(5) } }
        }
    };

    [Fact]
    public async Task Acknowledgement_Should_Advance_The_Offset_And_Resolve_The_Pending_Acknowledgement_Atomically()
    {
        // arrange
        var (handler, patches) = Create(CreateSubscription());

        // act
        var result = await handler.HandleAsync(new AcknowledgeSubscriptionEventCommand("test-subscription", Token, true));

        // assert
        Assert.Equal(200, result.Status);
        var operations = patches.Operations;
        Assert.Equal(OperationType.Test, operations[0].Op);
        Assert.Equal("/status/subscriber/pendingAck/token", operations[0].Path.ToString());
        Assert.Contains(operations, o => o.Op == OperationType.Remove && o.Path.ToString() == "/status/subscriber/pendingAck");
        Assert.Equal([42ul], patches.OffsetsWrittenTo());
        Assert.True(patches.Touched("/status/subscriber/lastAcknowledgedAt"));
    }

    [Fact]
    public async Task Negative_Acknowledgement_Should_Leave_The_Offset_Untouched_And_Report_The_Reason()
    {
        // arrange
        var (handler, patches) = Create(CreateSubscription());

        // act
        await handler.HandleAsync(new AcknowledgeSubscriptionEventCommand("test-subscription", Token, false, "database unavailable"));

        // assert
        Assert.False(patches.Touched("/status/stream/ackedOffset"));
        Assert.Contains("database unavailable", patches.ValuesWrittenTo("/status/subscriber/reason").Single().ToString());
        Assert.Contains(patches.Operations, o => o.Op == OperationType.Remove && o.Path.ToString() == "/status/subscriber/pendingAck");
    }

    [Theory]
    [InlineData("another-token")]
    [InlineData("the-pending-token-but-longer")]
    public async Task Unknown_Token_Should_Conflict(string token)
    {
        // arrange
        var (handler, patches) = Create(CreateSubscription());

        // act
        var result = await handler.HandleAsync(new AcknowledgeSubscriptionEventCommand("test-subscription", token, true));

        // assert
        Assert.Equal(409, result.Status);
        Assert.Equal(0, patches.Count);
    }

    [Fact]
    public async Task Expired_Acknowledgement_Should_Conflict()
    {
        // arrange
        var (handler, patches) = Create(CreateSubscription(DateTimeOffset.UtcNow.AddSeconds(-1)));

        // act
        var result = await handler.HandleAsync(new AcknowledgeSubscriptionEventCommand("test-subscription", Token, true));

        // assert
        Assert.Equal(409, result.Status);
        Assert.Contains("expired", result.Errors!.First().Detail);
        Assert.Equal(0, patches.Count);
    }

    [Fact]
    public async Task Unknown_Subscription_Should_Conflict_Like_An_Invalid_Token()
    {
        // arrange
        var (handler, _) = Create(null);

        // act - the endpoint is anonymous, so it must not reveal which subscriptions exist (see #149)
        var result = await handler.HandleAsync(new AcknowledgeSubscriptionEventCommand("missing", Token, true));

        // assert
        Assert.Equal(409, result.Status);
    }

    [Fact]
    public async Task The_Hash_Stored_In_The_Status_Should_Not_Be_Usable_As_A_Token()
    {
        // arrange
        var (handler, patches) = Create(CreateSubscription());

        // act - anyone allowed to read the subscription sees the hash, not the token
        var result = await handler.HandleAsync(new AcknowledgeSubscriptionEventCommand("test-subscription", AcknowledgementTokens.Hash(Token), true));

        // assert
        Assert.Equal(409, result.Status);
        Assert.Equal(0, patches.Count);
    }

}
