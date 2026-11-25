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

using CloudStreams.Core.Api.Services;
using Microsoft.Extensions.Logging.Abstractions;
using GatewayResource = CloudStreams.Core.Resources.Gateway;

namespace CloudStreams.UnitTests.Cases.Core;

public class ServiceHealthMonitorTests
{

    sealed class Context
        : IAsyncDisposable
    {

        readonly Subject<IResourceWatchEvent<GatewayResource>> _events = new();

        public Context(IHealthMonitorLeaseManager? leases = null)
        {
            this.Gateway = new GatewayResource(new ResourceMetadata("gateway"), new GatewaySpec() { Service = new(new Uri("http://gateway.local")) });
            var monitor = Substitute.For<IResourceMonitor<GatewayResource>>();
            monitor.Resource.Returns(_ => this.Gateway);
            monitor.Subscribe(Arg.Any<IObserver<IResourceWatchEvent<GatewayResource>>>()).Returns(call => this._events.Subscribe(call.Arg<IObserver<IResourceWatchEvent<GatewayResource>>>()!));
            var repository = Substitute.For<IResourceRepository>();
            repository.PatchSubResourceAsync(Arg.Any<Patch>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
                .Returns(_ => Task.FromResult<IResource>(this.Gateway));
            this.Http = StubHttpMessageHandler.AlwaysResponds(HttpStatusCode.OK);
            this.Monitor = new GatewayHealthMonitor(NullLogger<GatewayHealthMonitor>.Instance, Neuroglia.Serialization.Json.JsonSerializer.Default, repository, monitor, this.Http.CreateClient(), leases ?? new InMemoryLeaseManager());
        }

        public GatewayResource Gateway { get; private set; }

        public StubHttpMessageHandler Http { get; }

        public GatewayHealthMonitor Monitor { get; }

        public void Update(Action<GatewayResource> update)
        {
            var gateway = this.Gateway.Clone()!;
            update(gateway);
            this.Gateway = gateway;
            this._events.OnNext(new ResourceWatchEvent<GatewayResource>(ResourceWatchEventType.Updated, gateway));
        }

        public async ValueTask DisposeAsync()
        {
            await this.Monitor.DisposeAsync();
            this._events.Dispose();
            this.Http.Dispose();
        }

    }

    static async Task<bool> EventuallyAsync(Func<bool> condition, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(5));
        while (DateTime.UtcNow < deadline)
        {
            if (condition()) return true;
            await Task.Delay(20);
        }
        return condition();
    }

    static ServiceHealthCheckConfiguration HealthChecks(TimeSpan interval) => new(new HttpRequestConfiguration("GET", "/healthz"), Duration.FromTimeSpan(interval));

    [Fact]
    public async Task Monitoring_Should_Start_When_Health_Checks_Are_Configured_After_Creation()
    {
        // arrange
        await using var context = new Context();
        await context.Monitor.StartAsync(CancellationToken.None);
        await Task.Delay(100);
        Assert.Equal(0, context.Http.AttemptCount);

        // act
        context.Update(g => g.Spec.Service!.HealthChecks = HealthChecks(TimeSpan.FromHours(1)));

        // assert
        Assert.True(await EventuallyAsync(() => context.Http.AttemptCount == 1), $"Expected a health check, got {context.Http.AttemptCount}");
        Assert.Equal(new Uri("http://gateway.local/healthz"), context.Http.Attempts[0].RequestUri);
    }

    [Fact]
    public async Task A_Stopped_Monitor_Should_Not_Check_Health_Anymore()
    {
        // arrange
        await using var context = new Context();
        context.Update(g => g.Spec.Service!.HealthChecks = HealthChecks(TimeSpan.FromMilliseconds(50)));
        await context.Monitor.StartAsync(CancellationToken.None);
        Assert.True(await EventuallyAsync(() => context.Http.AttemptCount >= 2));

        // act
        await context.Monitor.StopAsync(CancellationToken.None);
        await Task.Delay(100);
        var attempts = context.Http.AttemptCount;
        await Task.Delay(300);

        // assert
        Assert.Equal(attempts, context.Http.AttemptCount);
    }

    [Fact]
    public async Task The_First_Check_Should_Honour_The_Time_Elapsed_Since_The_Last_One()
    {
        // arrange
        await using var context = new Context();
        context.Update(g =>
        {
            g.Spec.Service!.HealthChecks = HealthChecks(TimeSpan.FromHours(1));
            g.Status = new() { HealthStatus = "healthy", LastHealthCheckAt = DateTimeOffset.UtcNow };
        });

        // act
        await context.Monitor.StartAsync(CancellationToken.None);
        await Task.Delay(300);

        // assert
        Assert.Equal(0, context.Http.AttemptCount);
    }

    [Fact]
    public async Task A_Single_Process_Should_Check_The_Health_Of_A_Service_At_A_Time()
    {
        // arrange - two replicas monitor the same gateway, and share their leases as they would through Redis
        var leases = new InMemoryLeaseManager();
        await using var first = new Context(leases.ForOwner("first"));
        await using var second = new Context(leases.ForOwner("second"));
        first.Update(g => g.Spec.Service!.HealthChecks = HealthChecks(TimeSpan.FromMilliseconds(50)));
        second.Update(g => g.Spec.Service!.HealthChecks = HealthChecks(TimeSpan.FromMilliseconds(50)));

        // act
        await first.Monitor.StartAsync(CancellationToken.None);
        Assert.True(await EventuallyAsync(() => first.Http.AttemptCount >= 1));
        await second.Monitor.StartAsync(CancellationToken.None);
        await Task.Delay(400);

        // assert
        Assert.True(first.Http.AttemptCount >= 3, $"Expected the lease owner to keep checking, got {first.Http.AttemptCount}");
        Assert.Equal(0, second.Http.AttemptCount);
    }

    [Fact]
    public async Task Another_Process_Should_Take_Over_When_The_Lease_Owner_Stops()
    {
        // arrange
        var leases = new InMemoryLeaseManager();
        await using var first = new Context(leases.ForOwner("first"));
        await using var second = new Context(leases.ForOwner("second"));
        first.Update(g => g.Spec.Service!.HealthChecks = HealthChecks(TimeSpan.FromMilliseconds(50)));
        second.Update(g => g.Spec.Service!.HealthChecks = HealthChecks(TimeSpan.FromMilliseconds(50)));
        await first.Monitor.StartAsync(CancellationToken.None);
        Assert.True(await EventuallyAsync(() => first.Http.AttemptCount >= 1));
        await second.Monitor.StartAsync(CancellationToken.None);

        // act - stopping releases the lease, rather than leaving the service unmonitored until it expires
        await first.Monitor.StopAsync(CancellationToken.None);

        // assert
        Assert.True(await EventuallyAsync(() => second.Http.AttemptCount >= 1), "Expected the second replica to take over");
        Assert.False(leases.IsHeldBy("gateway:gateway", "first"));
    }

    /// <summary>
    /// Stands for the leases stored in Redis
    /// </summary>
    sealed class InMemoryLeaseManager(string owner = "default", Dictionary<string, (string Owner, DateTimeOffset ExpiresAt)>? leases = null)
        : IHealthMonitorLeaseManager
    {

        readonly Dictionary<string, (string Owner, DateTimeOffset ExpiresAt)> _leases = leases ?? [];

        public string OwnerId { get; } = owner;

        public InMemoryLeaseManager ForOwner(string owner) => new(owner, this._leases);

        public bool IsHeldBy(string key, string owner)
        {
            lock (this._leases) return this._leases.TryGetValue(key, out var lease) && lease.Owner == owner && lease.ExpiresAt > DateTimeOffset.UtcNow;
        }

        public Task<bool> TryAcquireAsync(string key, TimeSpan duration, CancellationToken cancellationToken = default)
        {
            lock (this._leases)
            {
                if (this._leases.TryGetValue(key, out var lease) && lease.Owner != this.OwnerId && lease.ExpiresAt > DateTimeOffset.UtcNow) return Task.FromResult(false);
                this._leases[key] = (this.OwnerId, DateTimeOffset.UtcNow + duration);
                return Task.FromResult(true);
            }
        }

        public Task ReleaseAsync(string key, CancellationToken cancellationToken = default)
        {
            lock (this._leases) if (this._leases.TryGetValue(key, out var lease) && lease.Owner == this.OwnerId) this._leases.Remove(key);
            return Task.CompletedTask;
        }

    }

}
