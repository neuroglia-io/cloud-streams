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
using Neuroglia.Data.Infrastructure.EventSourcing;
using System.Text.Json.Nodes;

namespace CloudStreams.UnitTests.Cases.Core;

/// <summary>
/// Covers the dedicated commands that let operators resume and replay subscriptions without being allowed to patch them (issue #147)
/// </summary>
public class OperateSubscriptionCommandTests
{

    /// <summary>
    /// Stands for the resource repository: it applies JSON patches to a single subscription, and records them
    /// </summary>
    sealed class SubscriptionStore
    {

        public SubscriptionStore(Subscription subscription)
        {
            this.Current = subscription;
            this.Repository.GetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
                .Returns(_ => Task.FromResult<IResource?>(this.Current));
            this.Repository.PatchAsync(Arg.Any<Patch>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
                .Returns(call => Task.FromResult<IResource>(this.Apply(call.Arg<Patch>(), this.SpecPatches)));
            this.Repository.PatchSubResourceAsync(Arg.Any<Patch>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
                .Returns(call => Task.FromResult<IResource>(this.Apply(call.Arg<Patch>(), this.StatusPatches)));
        }

        public IResourceRepository Repository { get; } = Substitute.For<IResourceRepository>();

        public Subscription Current { get; private set; }

        public List<JsonPatch> SpecPatches { get; } = [];

        public List<JsonPatch> StatusPatches { get; } = [];

        /// <summary>
        /// Gets/sets the number of the spec patch, counting from 1, that fails once, if any
        /// </summary>
        public int? FailingSpecPatch { get; set; }

        public List<long?> Offsets { get; } = [];

        Subscription Apply(Patch patch, List<JsonPatch> patches)
        {
            var jsonPatch = (JsonPatch)patch.Document;
            patches.Add(jsonPatch);
            if (patches == this.SpecPatches && patches.Count == this.FailingSpecPatch) throw new ProblemDetailsException(new(new("https://cloud-streams.io/problems/test"), "Unavailable", 503));
            var node = Neuroglia.Serialization.Json.JsonSerializer.Default.SerializeToNode(this.Current)!;
            var result = jsonPatch.Apply(node);
            if (!result.IsSuccess) throw new ProblemDetailsException(new(new("https://cloud-streams.io/problems/patch-conflict"), "Conflict", 409, result.Error));
            this.Current = Neuroglia.Serialization.Json.JsonSerializer.Default.Deserialize<Subscription>(result.Result!)!;
            if (patches == this.SpecPatches) this.Offsets.Add(this.Current.Spec.Stream?.Offset);
            return this.Current;
        }

    }

    static Subscription CreateSubscription(long? offset, bool faulted = false) => new()
    {
        Metadata = new() { Name = "orders", Generation = 1 },
        Spec = new() { Subscriber = new() { Uri = new("http://localhost:8888/events") }, Stream = offset.HasValue ? new() { Offset = offset } : null },
        Status = new() { Stream = faulted ? new() { AckedOffset = 3, Fault = new(new("https://cloud-streams.io/problems/test"), "Boom", 500) } : null }
    };

    [Fact]
    public async Task Resuming_Should_Clear_The_Fault_Only()
    {
        // arrange
        var store = new SubscriptionStore(CreateSubscription(0, faulted: true));

        // act
        var result = await new ResumeSubscriptionCommandHandler(store.Repository).HandleAsync(new ResumeSubscriptionCommand("orders"));

        // assert
        Assert.Equal(200, result.Status);
        var operation = Assert.Single(Assert.Single(store.StatusPatches).Operations);
        Assert.Equal(OperationType.Remove, operation.Op);
        Assert.Equal("/status/stream/fault", operation.Path.ToString());
        Assert.Null(store.Current.Status!.Stream!.Fault);
        Assert.Equal(3ul, store.Current.Status.Stream.AckedOffset);
        Assert.Empty(store.SpecPatches);
    }

    [Fact]
    public async Task Resuming_A_Subscription_That_Is_Not_Faulted_Should_Change_Nothing()
    {
        // arrange
        var store = new SubscriptionStore(CreateSubscription(0));

        // act
        var result = await new ResumeSubscriptionCommandHandler(store.Repository).HandleAsync(new ResumeSubscriptionCommand("orders"));

        // assert
        Assert.Equal(200, result.Status);
        Assert.Empty(store.StatusPatches);
    }

    [Fact]
    public async Task Suspending_Should_Only_Set_The_Suspended_Flag()
    {
        // arrange
        var store = new SubscriptionStore(CreateSubscription(42));

        // act
        var result = await new SuspendSubscriptionCommandHandler(store.Repository).HandleAsync(new SuspendSubscriptionCommand("orders"));

        // assert
        Assert.Equal(200, result.Status);
        var operation = Assert.Single(Assert.Single(store.SpecPatches).Operations);
        Assert.Equal("/spec/suspended", operation.Path.ToString());
        Assert.True(store.Current.Spec.Suspended);
        Assert.Equal(42, store.Current.Spec.Stream!.Offset);
        Assert.Empty(store.StatusPatches);
    }

    [Fact]
    public async Task Suspending_A_Suspended_Subscription_Should_Change_Nothing()
    {
        // arrange
        var subscription = CreateSubscription(42);
        subscription.Spec.Suspended = true;
        var store = new SubscriptionStore(subscription);

        // act
        var result = await new SuspendSubscriptionCommandHandler(store.Repository).HandleAsync(new SuspendSubscriptionCommand("orders"));

        // assert
        Assert.Equal(200, result.Status);
        Assert.Empty(store.SpecPatches);
    }

    [Fact]
    public async Task Suspending_A_Missing_Subscription_Should_Return_Not_Found()
    {
        // arrange
        var store = new SubscriptionStore(CreateSubscription(42));
        store.Repository.GetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult<IResource?>(null));

        // act
        var result = await new SuspendSubscriptionCommandHandler(store.Repository).HandleAsync(new SuspendSubscriptionCommand("orders"));

        // assert
        Assert.Equal(404, result.Status);
    }

    [Fact]
    public async Task Resuming_A_Suspended_Subscription_Should_Lift_The_Suspension()
    {
        // arrange
        var subscription = CreateSubscription(42);
        subscription.Spec.Suspended = true;
        var store = new SubscriptionStore(subscription);

        // act
        var result = await new ResumeSubscriptionCommandHandler(store.Repository).HandleAsync(new ResumeSubscriptionCommand("orders"));

        // assert
        Assert.Equal(200, result.Status);
        Assert.False(store.Current.Spec.Suspended);
        Assert.Single(store.SpecPatches);
        Assert.Empty(store.StatusPatches);
    }

    [Fact]
    public async Task Resuming_A_Suspended_And_Faulted_Subscription_Should_Lift_The_Suspension_And_Clear_The_Fault()
    {
        // arrange
        var subscription = CreateSubscription(0, faulted: true);
        subscription.Spec.Suspended = true;
        var store = new SubscriptionStore(subscription);

        // act
        var result = await new ResumeSubscriptionCommandHandler(store.Repository).HandleAsync(new ResumeSubscriptionCommand("orders"));

        // assert
        Assert.Equal(200, result.Status);
        Assert.False(store.Current.Spec.Suspended);
        Assert.Null(store.Current.Status!.Stream!.Fault);
        Assert.Equal(3ul, store.Current.Status.Stream.AckedOffset);
    }

    [Fact]
    public async Task Replaying_Should_Move_The_Offset_To_The_Start_Of_The_Stream()
    {
        // arrange
        var store = new SubscriptionStore(CreateSubscription(42));

        // act
        var result = await new ReplaySubscriptionCommandHandler(store.Repository).HandleAsync(new ReplaySubscriptionCommand("orders"));

        // assert
        Assert.Equal(200, result.Status);
        Assert.Equal([StreamPosition.StartOfStream], store.Offsets);
        Assert.Empty(store.StatusPatches);
    }

    [Fact]
    public async Task Replaying_A_Subscription_Without_Stream_Should_Add_One_That_Starts_At_The_Start_Of_The_Stream()
    {
        // arrange
        var store = new SubscriptionStore(CreateSubscription(null));

        // act
        await new ReplaySubscriptionCommandHandler(store.Repository).HandleAsync(new ReplaySubscriptionCommand("orders"));

        // assert
        Assert.Equal([StreamPosition.StartOfStream], store.Offsets);
    }

    [Fact]
    public async Task Replaying_A_Subscription_That_Starts_At_The_Start_Of_The_Stream_Should_Change_Its_Offset_Twice()
    {
        // arrange - the broker only reacts to changes of the offset (see #196)
        var store = new SubscriptionStore(CreateSubscription(StreamPosition.StartOfStream));

        // act
        await new ReplaySubscriptionCommandHandler(store.Repository).HandleAsync(new ReplaySubscriptionCommand("orders"));

        // assert
        Assert.Equal([StreamPosition.EndOfStream, StreamPosition.StartOfStream], store.Offsets);
    }

    [Fact]
    public async Task A_Failed_Replay_Should_Not_Leave_The_Subscription_At_The_End_Of_The_Stream()
    {
        // arrange
        var store = new SubscriptionStore(CreateSubscription(StreamPosition.StartOfStream)) { FailingSpecPatch = 2 };

        // act
        await Assert.ThrowsAsync<ProblemDetailsException>(() => new ReplaySubscriptionCommandHandler(store.Repository).HandleAsync(new ReplaySubscriptionCommand("orders")));

        // assert
        Assert.Equal(3, store.SpecPatches.Count);
        Assert.Equal(StreamPosition.StartOfStream, store.Current.Spec.Stream!.Offset);
    }

}
