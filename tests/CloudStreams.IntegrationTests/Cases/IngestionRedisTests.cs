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

using CloudStreams.Core.Resources;
using CloudStreams.Gateway.Application.Services;
using Neuroglia.Eventing.CloudEvents;
using StackExchange.Redis;
using Testcontainers.Redis;
using Xunit;

namespace CloudStreams.IntegrationTests.Cases;

/// <summary>
/// Verifies, against a real Redis, the deduplication and rate limiting of the cloud events ingested by gateways (#209, #210)
/// </summary>
public sealed class IngestionRedisTests
    : IAsyncLifetime
{

    readonly RedisContainer _redis = new RedisBuilder().WithImage("redis:7.4-alpine").Build();
    ConnectionMultiplexer _connection = null!;

    public async Task InitializeAsync()
    {
        await this._redis.StartAsync();
        this._connection = await ConnectionMultiplexer.ConnectAsync(this._redis.GetConnectionString());
    }

    public async Task DisposeAsync()
    {
        await this._connection.DisposeAsync();
        await this._redis.DisposeAsync();
    }

    static CloudEvent Event(string id, string source = "https://orders.example.com") => new() { Id = id, SpecVersion = "1.0", Source = new(source), Type = "order.placed" };

    [Fact]
    public async Task A_Reserved_Event_Should_Be_In_Flight_Until_It_Is_Confirmed_Or_Released()
    {
        // arrange
        var deduplicator = new RedisCloudEventDeduplicator(this._connection);
        var e = Event("1");

        // act and assert
        Assert.Equal(DeduplicationOutcome.Reserved, await deduplicator.TryReserveAsync("gateway", e));
        Assert.Equal(DeduplicationOutcome.InFlight, await deduplicator.TryReserveAsync("gateway", e));
        await deduplicator.ReleaseAsync("gateway", e);
        Assert.Equal(DeduplicationOutcome.Reserved, await deduplicator.TryReserveAsync("gateway", e));
        await deduplicator.ConfirmAsync("gateway", e, TimeSpan.FromMinutes(1));
        Assert.Equal(DeduplicationOutcome.Duplicate, await deduplicator.TryReserveAsync("gateway", e));
    }

    [Fact]
    public async Task A_Confirmed_Event_Should_Be_Forgotten_Once_The_Window_Has_Elapsed()
    {
        // arrange
        var deduplicator = new RedisCloudEventDeduplicator(this._connection);
        var e = Event("1");
        await deduplicator.TryReserveAsync("gateway", e);
        await deduplicator.ConfirmAsync("gateway", e, TimeSpan.FromMilliseconds(300));

        // act
        await Task.Delay(600);

        // assert
        Assert.Equal(DeduplicationOutcome.Reserved, await deduplicator.TryReserveAsync("gateway", e));
    }

    [Fact]
    public async Task Events_Should_Be_Identified_By_Their_Source_And_Id_Within_Each_Gateway()
    {
        // arrange
        var deduplicator = new RedisCloudEventDeduplicator(this._connection);
        await deduplicator.TryReserveAsync("gateway", Event("1"));
        await deduplicator.ConfirmAsync("gateway", Event("1"), TimeSpan.FromMinutes(1));

        // act and assert
        Assert.Equal(DeduplicationOutcome.Reserved, await deduplicator.TryReserveAsync("gateway", Event("1", "https://invoices.example.com")));
        Assert.Equal(DeduplicationOutcome.Reserved, await deduplicator.TryReserveAsync("gateway", Event("2")));
        Assert.Equal(DeduplicationOutcome.Reserved, await deduplicator.TryReserveAsync("other-gateway", Event("1")));
    }

    [Fact]
    public async Task Events_Beyond_The_Limit_Should_Be_Refused_Until_The_Window_Elapses()
    {
        // arrange
        var limiter = new RedisCloudEventRateLimiter(this._connection);
        IReadOnlyList<CloudEventRateLimit> limits = [new() { Partition = CloudEventRateLimitPartitioning.Publisher, Limit = 3, Period = Duration.FromHours(1) }];

        // act
        var decisions = new List<RateLimitDecision>();
        for (var i = 0; i < 4; i++) decisions.Add(await limiter.EvaluateAsync("gateway", Event($"{i}"), "orders", limits));

        // assert
        Assert.Equal([true, true, true, false], decisions.Select(d => d.Allowed));
        Assert.InRange(decisions[3].RetryAfter!.Value, TimeSpan.FromMilliseconds(1), TimeSpan.FromHours(1));
        // other publishers, anonymous ones included, and other gateways have limits of their own
        Assert.True((await limiter.EvaluateAsync("gateway", Event("4"), "invoices", limits)).Allowed);
        Assert.True((await limiter.EvaluateAsync("gateway", Event("5"), null, limits)).Allowed);
        Assert.True((await limiter.EvaluateAsync("other-gateway", Event("6"), "orders", limits)).Allowed);
    }

    [Fact]
    public async Task An_Event_Should_Be_Within_Every_Limit()
    {
        // arrange - each source may publish 2 events per hour, and each publisher 10
        var limiter = new RedisCloudEventRateLimiter(this._connection);
        IReadOnlyList<CloudEventRateLimit> limits =
        [
            new() { Partition = CloudEventRateLimitPartitioning.Publisher, Limit = 10, Period = Duration.FromHours(1) },
            new() { Partition = CloudEventRateLimitPartitioning.Source, Limit = 2, Period = Duration.FromHours(1) }
        ];

        // act
        var allowed = new List<bool>();
        for (var i = 0; i < 3; i++) allowed.Add((await limiter.EvaluateAsync("gateway", Event($"{i}"), "orders", limits)).Allowed);
        var otherSource = await limiter.EvaluateAsync("gateway", Event("3", "https://invoices.example.com"), "orders", limits);

        // assert
        Assert.Equal([true, true, false], allowed);
        Assert.True(otherSource.Allowed);
    }

    [Fact]
    public async Task The_Counter_Should_Restart_With_Each_Window()
    {
        // arrange
        var limiter = new RedisCloudEventRateLimiter(this._connection);
        IReadOnlyList<CloudEventRateLimit> limits = [new() { Partition = CloudEventRateLimitPartitioning.Publisher, Limit = 1, Period = Duration.FromMilliseconds(500) }];
        // the first event is published at the start of a window, so that the second one is in the same window
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        await Task.Delay(TimeSpan.FromMilliseconds(500 - now % 500 + 10));

        // act
        var first = await limiter.EvaluateAsync("gateway", Event("1"), "orders", limits);
        var second = await limiter.EvaluateAsync("gateway", Event("2"), "orders", limits);
        await Task.Delay(second.RetryAfter ?? TimeSpan.Zero);
        var third = await limiter.EvaluateAsync("gateway", Event("3"), "orders", limits);

        // assert
        Assert.True(first.Allowed);
        Assert.False(second.Allowed);
        Assert.True(third.Allowed);
    }

}
