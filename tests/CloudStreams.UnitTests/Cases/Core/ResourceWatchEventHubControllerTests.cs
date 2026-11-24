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

using CloudStreams.Core.Api.Client.Services;
using CloudStreams.Core.Api.Hubs;
using CloudStreams.Core.Api.Services;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace CloudStreams.UnitTests.Cases.Core;

public class ResourceWatchEventHubControllerTests
{

    static (ResourceWatchEventHubController Controller, ConcurrentBag<IResourceWatch> Watches) Create()
    {
        var watches = new ConcurrentBag<IResourceWatch>();
        var repository = Substitute.For<IResourceRepository>();
        repository.WatchAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<IEnumerable<LabelSelector>?>(), Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                // gives concurrent calls the opportunity to interleave
                await Task.Delay(50);
                var watch = Substitute.For<IResourceWatch>();
                watches.Add(watch);
                return watch;
            });
        return (new ResourceWatchEventHubController(repository, Substitute.For<IHubContext<ResourceEventWatchHub, IResourceEventWatchHubClient>>()), watches);
    }

    static ResourceDefinitionInfo Definition(string plural) => new("cloud-streams.io", "v1", plural, plural);

    [Fact]
    public async Task Concurrent_Watches_Of_A_Connection_Should_All_Be_Released_With_It()
    {
        // arrange
        var (controller, watches) = Create();

        // act
        await Task.WhenAll(
            controller.WatchResourcesAsync("connection", Definition("subscriptions")),
            controller.WatchResourcesAsync("connection", Definition("brokers")),
            controller.WatchResourcesAsync("connection", Definition("gateways")));
        await controller.ReleaseConnectionResourcesAsync("connection");

        // assert
        Assert.Equal(3, watches.Count);
        foreach (var watch in watches) watch.Received(1).Dispose();
    }

    [Fact]
    public async Task Concurrent_Watches_Of_The_Same_Resources_Should_Not_Leak()
    {
        // arrange
        var (controller, watches) = Create();

        // act
        await Task.WhenAll(
            controller.WatchResourcesAsync("connection", Definition("subscriptions")),
            controller.WatchResourcesAsync("connection", Definition("subscriptions")));
        await controller.ReleaseConnectionResourcesAsync("connection");

        // assert
        foreach (var watch in watches) watch.Received(1).Dispose();
    }

    [Fact]
    public async Task A_Connection_Should_Not_Watch_More_Than_The_Maximum_Number_Of_Resource_Types()
    {
        // arrange
        var (controller, _) = Create();
        for (var i = 0; i < ResourceWatchEventHubController.MaxWatchesPerConnection; i++) await controller.WatchResourcesAsync("connection", Definition($"kind{i}"));

        // act & assert
        await Assert.ThrowsAsync<HubException>(() => controller.WatchResourcesAsync("connection", Definition("one-too-many")));
    }

}
