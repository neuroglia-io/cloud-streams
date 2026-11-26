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

using CloudStreams.Core.Api.HealthChecks;
using CloudStreams.Core.Application;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Neuroglia.Data.Infrastructure.EventSourcing.Services;
using HealthStatus = Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus;

namespace CloudStreams.UnitTests.Cases.Core;

/// <summary>
/// Covers the fixes of the event store and health check issues (#186, #187, #201)
/// </summary>
public class StoreHealthTests
{

    static CloudEventStore Store(IProjectionManager projectionManager, IEventStore? eventStore = null) =>
        new(eventStore ?? Substitute.For<IEventStore>(), projectionManager, Neuroglia.Serialization.Json.JsonSerializer.Default);

    static IProjectionManager ProjectionManager(params string[] existing)
    {
        var manager = Substitute.For<IProjectionManager>();
        manager.GetStatusAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult<IProjectionStatus?>(existing.Contains(call.ArgAt<string>(0)) ? Substitute.For<IProjectionStatus>() : null));
        return manager;
    }

    // ---------- #186 ----------

    [Fact]
    public async Task Only_The_Missing_Projections_Should_Be_Created()
    {
        // arrange - the by-source projection exists, which used to skip the setup of every other projection
        var manager = ProjectionManager(ProjectionNameHelper.PartitionBySource);
        var store = Store(manager);

        // act
        var missing = await store.SetupProjectionsAsync();

        // assert
        Assert.Empty(missing);
        await manager.DidNotReceive().CreateAsync(ProjectionNameHelper.PartitionBySource, Arg.Any<Action<IProjectionSourceBuilder<object>>>(), Arg.Any<CancellationToken>());
        await manager.Received(1).CreateAsync(ProjectionNameHelper.PartitionBySubject, Arg.Any<Action<IProjectionSourceBuilder<object>>>(), Arg.Any<CancellationToken>());
        await manager.Received(1).CreateAsync(ProjectionNameHelper.PartitionByCausationId, Arg.Any<Action<IProjectionSourceBuilder<object>>>(), Arg.Any<CancellationToken>());
        // partition ids are listed by the partition index rather than by projections, whose state grows with every partition (see #185)
        await manager.DidNotReceive().CreateAsync(Arg.Is<string>(n => n!.StartsWith(ProjectionNameHelper.CloudEventPartitionsMetadataPrefix)), Arg.Any<Action<IProjectionSourceBuilder<List<object>>>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_Projection_Reported_As_Not_Found_Should_Be_Created()
    {
        // arrange - EventStoreDB reports a missing projection with a 'NotFound' RpcException rather than with a null status
        var manager = Substitute.For<IProjectionManager>();
        manager.GetStatusAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromException<IProjectionStatus?>(new Grpc.Core.RpcException(new Grpc.Core.Status(Grpc.Core.StatusCode.NotFound, $"Projection '{call.ArgAt<string>(0)}' not found"))));
        var store = Store(manager);

        // act
        var missing = await store.SetupProjectionsAsync();

        // assert
        Assert.Empty(missing);
        await manager.Received(1).CreateAsync(ProjectionNameHelper.PartitionBySource, Arg.Any<Action<IProjectionSourceBuilder<object>>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_Projection_That_Cannot_Be_Created_Should_Be_Reported_Without_Preventing_The_Others()
    {
        // arrange
        var manager = ProjectionManager();
        manager.CreateAsync(ProjectionNameHelper.PartitionBySubject, Arg.Any<Action<IProjectionSourceBuilder<object>>>(), Arg.Any<CancellationToken>()).Returns(Task.FromException(new InvalidOperationException("boom")));
        var store = Store(manager);

        // act
        var missing = await store.SetupProjectionsAsync();

        // assert
        Assert.Equal([ProjectionNameHelper.PartitionBySubject], missing);
        Assert.Equal([ProjectionNameHelper.PartitionBySubject], store.MissingProjections);
        await manager.Received(1).CreateAsync(ProjectionNameHelper.PartitionByCausationId, Arg.Any<Action<IProjectionSourceBuilder<object>>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_Projection_Created_Concurrently_By_Another_Application_Should_Not_Be_Reported_Missing()
    {
        // arrange: the projection does not exist when checked, creating it conflicts with another application's creation, and it exists afterwards
        var created = false;
        var manager = Substitute.For<IProjectionManager>();
        manager.GetStatusAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult<IProjectionStatus?>(call.ArgAt<string>(0) == ProjectionNameHelper.PartitionBySubject && created ? Substitute.For<IProjectionStatus>() : null));
        manager.CreateAsync(ProjectionNameHelper.PartitionBySubject, Arg.Any<Action<IProjectionSourceBuilder<object>>>(), Arg.Any<CancellationToken>())
            .Returns(_ => { created = true; return Task.FromException(new InvalidOperationException("Envelope callback expected Updated, received Conflict instead")); });
        var store = Store(manager);

        // act
        var missing = await store.SetupProjectionsAsync();

        // assert
        Assert.Empty(missing);
        Assert.Empty(store.MissingProjections ?? []);
    }

    // ---------- #187 ----------

    [Fact]
    public async Task Listing_Partitions_Without_Their_Projection_Should_Yield_Nothing()
    {
        // arrange
        var store = Store(ProjectionManager());

        // act
        var ids = await store.ListPartitionIdsAsync(CloudEventPartitionType.BySource).ToListAsync();

        // assert
        Assert.Empty(ids);
    }

    [Fact]
    public async Task Listing_Partitions_Should_Report_A_Failure_Rather_Than_An_Empty_List()
    {
        // arrange
        var manager = Substitute.For<IProjectionManager>();
        manager.GetStatusAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.FromException<IProjectionStatus?>(new InvalidOperationException("the event store is down")));
        var store = Store(manager);

        // act
        var exception = await Assert.ThrowsAsync<ProblemDetailsException>(() => store.ListPartitionIdsAsync(CloudEventPartitionType.BySource).ToListAsync().AsTask());

        // assert
        Assert.Equal((int)HttpStatusCode.ServiceUnavailable, exception.Problem.Status);
    }

    // ---------- #201 ----------

    [Fact]
    public async Task The_Projections_Health_Check_Should_Reflect_The_Setup_State()
    {
        // arrange
        var failing = ProjectionManager();
        failing.CreateAsync(ProjectionNameHelper.PartitionBySubject, Arg.Any<Action<IProjectionSourceBuilder<object>>>(), Arg.Any<CancellationToken>()).Returns(Task.FromException(new InvalidOperationException("boom")));
        var notSetUp = Store(ProjectionManager());
        var faulty = Store(failing);
        await faulty.SetupProjectionsAsync();
        var healthy = Store(ProjectionManager());
        await healthy.SetupProjectionsAsync();

        // act & assert
        Assert.Equal(HealthStatus.Degraded, (await new ProjectionsHealthCheck(notSetUp).CheckHealthAsync(new())).Status);
        Assert.Equal(HealthStatus.Unhealthy, (await new ProjectionsHealthCheck(faulty).CheckHealthAsync(new())).Status);
        Assert.Equal(HealthStatus.Healthy, (await new ProjectionsHealthCheck(healthy).CheckHealthAsync(new())).Status);
    }

    [Fact]
    public async Task The_Event_Store_Health_Check_Should_Be_Unhealthy_When_The_Store_Is_Unreachable()
    {
        // arrange
        var eventStore = Substitute.For<ICloudEventStore>();
        eventStore.GetStreamMetadataAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(Task.FromException<StreamMetadata>(new InvalidOperationException("unreachable")));

        // act
        var result = await new EventStoreHealthCheck(eventStore).CheckHealthAsync(new());

        // assert
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

}
