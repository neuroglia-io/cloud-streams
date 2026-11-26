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
                .Returns(call => Task.FromResult<IResource>(this.Apply(call.Arg<Patch>()!, this.SpecPatches)));
            this.Repository.PatchSubResourceAsync(Arg.Any<Patch>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
                .Returns(call => Task.FromResult<IResource>(this.Apply(call.Arg<Patch>()!, this.StatusPatches)));
        }

        public IResourceRepository Repository { get; } = Substitute.For<IResourceRepository>();

        public CloudStreams.Core.Application.Services.ICloudEventStore EventStore { get; } = Substitute.For<CloudStreams.Core.Application.Services.ICloudEventStore>();

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

    static Subscription CreateSubscription(long? offset, bool faulted = false, ulong? faultedOffset = null) => new()
    {
        Metadata = new() { Name = "orders", Generation = 1 },
        Spec = new() { Subscriber = new() { Endpoint = new Uri("http://localhost:8888/events") }, Stream = offset.HasValue ? new() { Offset = offset } : null },
        Status = new() { Stream = faulted ? new() { AckedOffset = 3, Fault = new(new("https://cloud-streams.io/problems/test"), "Boom", 500), FaultedOffset = faultedOffset } : null }
    };

    [Fact]
    public async Task Resuming_Should_Clear_The_Faulted_Offset_With_The_Fault()
    {
        // arrange
        var store = new SubscriptionStore(CreateSubscription(0, faulted: true, faultedOffset: 5));

        // act
        var result = await new ResumeSubscriptionCommandHandler(store.Repository).HandleAsync(new ResumeSubscriptionCommand("orders"));

        // assert
        Assert.Equal(200, result.Status);
        Assert.Equal(["/status/stream/fault", "/status/stream/faultedOffset"], Assert.Single(store.StatusPatches).Operations.Select(o => o.Path.ToString()));
        Assert.Null(store.Current.Status!.Stream!.Fault);
        Assert.Null(store.Current.Status.Stream.FaultedOffset);
    }

    [Fact]
    public async Task Skipping_Should_Move_The_Offset_Past_The_Faulted_Event()
    {
        // arrange
        var store = new SubscriptionStore(CreateSubscription(0, faulted: true, faultedOffset: 5));

        // act
        var result = await new SkipSubscriptionEventCommandHandler(store.Repository).HandleAsync(new SkipSubscriptionEventCommand("orders", 5));

        // assert - the broker clears the fault when the offset changes
        Assert.Equal(200, result.Status);
        Assert.Equal([6L], store.Offsets);
        Assert.Empty(store.StatusPatches);
    }

    [Fact]
    public async Task Skipping_To_The_Current_Offset_Should_Change_It_Twice()
    {
        // arrange
        var store = new SubscriptionStore(CreateSubscription(6, faulted: true, faultedOffset: 5));

        // act
        var result = await new SkipSubscriptionEventCommandHandler(store.Repository).HandleAsync(new SkipSubscriptionEventCommand("orders", 5));

        // assert - the broker only reacts to changes of the offset
        Assert.Equal(200, result.Status);
        Assert.Equal([StreamPosition.EndOfStream, 6L], store.Offsets);
    }

    [Theory]
    [InlineData(false, null, 5ul)]
    [InlineData(true, null, 5ul)]
    [InlineData(true, 7ul, 5ul)]
    public async Task Skipping_Should_Be_Refused_Unless_The_Event_Faulted_The_Subscription(bool faulted, ulong? faultedOffset, ulong offset)
    {
        // arrange
        var store = new SubscriptionStore(CreateSubscription(0, faulted, faultedOffset));

        // act
        var result = await new SkipSubscriptionEventCommandHandler(store.Repository).HandleAsync(new SkipSubscriptionEventCommand("orders", offset));

        // assert
        Assert.Equal(409, result.Status);
        Assert.Empty(store.SpecPatches);
    }

    [Fact]
    public async Task Skipping_For_A_Missing_Subscription_Should_Return_Not_Found()
    {
        // arrange
        var repository = Substitute.For<IResourceRepository>();
        repository.GetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult<IResource?>(null));

        // act
        var result = await new SkipSubscriptionEventCommandHandler(repository).HandleAsync(new SkipSubscriptionEventCommand("missing", 5));

        // assert
        Assert.Equal(404, result.Status);
    }

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
        var result = await new ReplaySubscriptionCommandHandler(store.Repository, store.EventStore).HandleAsync(new ReplaySubscriptionCommand("orders"));

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
        await new ReplaySubscriptionCommandHandler(store.Repository, store.EventStore).HandleAsync(new ReplaySubscriptionCommand("orders"));

        // assert
        Assert.Equal([StreamPosition.StartOfStream], store.Offsets);
    }

    [Fact]
    public async Task Replaying_A_Subscription_That_Starts_At_The_Start_Of_The_Stream_Should_Change_Its_Offset_Twice()
    {
        // arrange - the broker only reacts to changes of the offset (see #196)
        var store = new SubscriptionStore(CreateSubscription(StreamPosition.StartOfStream));

        // act
        await new ReplaySubscriptionCommandHandler(store.Repository, store.EventStore).HandleAsync(new ReplaySubscriptionCommand("orders"));

        // assert
        Assert.Equal([StreamPosition.EndOfStream, StreamPosition.StartOfStream], store.Offsets);
    }

    [Fact]
    public async Task A_Failed_Replay_Should_Not_Leave_The_Subscription_At_The_End_Of_The_Stream()
    {
        // arrange
        var store = new SubscriptionStore(CreateSubscription(StreamPosition.StartOfStream)) { FailingSpecPatch = 2 };

        // act
        await Assert.ThrowsAsync<ProblemDetailsException>(() => new ReplaySubscriptionCommandHandler(store.Repository, store.EventStore).HandleAsync(new ReplaySubscriptionCommand("orders")));

        // assert
        Assert.Equal(3, store.SpecPatches.Count);
        Assert.Equal(StreamPosition.StartOfStream, store.Current.Spec.Stream!.Offset);
    }

    static readonly DateTimeOffset From = new(2026, 11, 25, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Replaying_From_A_Time_Should_Move_The_Offset_To_The_First_Event_Recorded_At_Or_After_It()
    {
        // arrange
        var subscription = CreateSubscription(42);
        subscription.Spec.Partition = new(CloudEventPartitionType.BySource, "https://orders.example.com");
        var store = new SubscriptionStore(subscription);
        store.EventStore.FindOffsetAsync(Arg.Any<string?>(), From, subscription.Spec.Partition, Arg.Any<CancellationToken>()).Returns((ulong?)17);

        // act
        var result = await new ReplaySubscriptionCommandHandler(store.Repository, store.EventStore).HandleAsync(new ReplaySubscriptionCommand("orders", From));

        // assert - the time is resolved in the partition the subscription consumes
        Assert.Equal(200, result.Status);
        Assert.Equal([17L], store.Offsets);
    }

    [Fact]
    public async Task Replaying_From_A_Time_After_The_Last_Event_Should_Start_With_The_Next_Event()
    {
        // arrange
        var store = new SubscriptionStore(CreateSubscription(StreamPosition.StartOfStream));
        store.EventStore.FindOffsetAsync(Arg.Any<string?>(), From, null, Arg.Any<CancellationToken>()).Returns((ulong?)null);
        store.EventStore.GetStreamMetadataAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(new StreamMetadata() { Length = 25 });

        // act
        await new ReplaySubscriptionCommandHandler(store.Repository, store.EventStore).HandleAsync(new ReplaySubscriptionCommand("orders", From));

        // assert
        Assert.Equal([25L], store.Offsets);
    }

    [Fact]
    public async Task Replaying_From_A_Time_That_Resolves_To_The_Current_Offset_Should_Change_It_Twice()
    {
        // arrange - the broker only reacts to changes of the offset (see #196)
        var store = new SubscriptionStore(CreateSubscription(17));
        store.EventStore.FindOffsetAsync(Arg.Any<string?>(), From, null, Arg.Any<CancellationToken>()).Returns((ulong?)17);

        // act
        await new ReplaySubscriptionCommandHandler(store.Repository, store.EventStore).HandleAsync(new ReplaySubscriptionCommand("orders", From));

        // assert
        Assert.Equal([StreamPosition.EndOfStream, 17L], store.Offsets);
    }

}
