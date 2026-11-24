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

using CloudStreams.Core;
using CloudStreams.Core.Api.Services;
using CloudStreams.Core.Resources;
using Json.Patch;
using Json.Pointer;
using Microsoft.Extensions.Logging.Abstractions;
using Neuroglia;
using Neuroglia.Data;
using Neuroglia.Data.Infrastructure.ResourceOriented;
using Neuroglia.Data.PatchModel.Services;
using Neuroglia.Serialization.Json;
using StackExchange.Redis;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using Testcontainers.Redis;
using Xunit;

namespace CloudStreams.IntegrationTests.Cases;

/// <summary>
/// Verifies, against a real Redis, that the database applies updates atomically, so that concurrent writers never overwrite each other (#161)
/// </summary>
public sealed class AtomicRedisDatabaseTests
    : IAsyncLifetime
{

    readonly RedisContainer _redis = new RedisBuilder().WithImage("redis:7.4-alpine").Build();
    ConnectionMultiplexer _connection = null!;
    AtomicRedisDatabase _database = null!;

    static string Group => Subscription.ResourceDefinition.Group;

    static string Version => Subscription.ResourceDefinition.Version;

    static string Plural => Subscription.ResourceDefinition.Plural;

    public async Task InitializeAsync()
    {
        await this._redis.StartAsync();
        this._connection = await ConnectionMultiplexer.ConnectAsync(this._redis.GetConnectionString());
        this._database = new AtomicRedisDatabase(NullLoggerFactory.Instance, this._connection, JsonSerializer.Default, [new JsonPatchHandler(), new JsonMergePatchHandler()]);
    }

    public async Task DisposeAsync()
    {
        await this._database.DisposeAsync();
        await this._connection.DisposeAsync();
        await this._redis.DisposeAsync();
    }

    async Task<Subscription> CreateSubscriptionAsync([CallerMemberName] string name = "")
    {
        var subscription = new Subscription(new() { Name = name.ToLowerInvariant().Replace('_', '-'), Labels = new Dictionary<string, string>() }, new() { Subscriber = new() { Uri = new("http://localhost/events") } }, new()
        {
            Phase = SubscriptionStatusPhase.Active,
            Stream = new() { AckedOffset = 0 },
            Subscriber = new() { State = SubscriberState.Reachable }
        });
        return (await this._database.CreateResourceAsync(subscription, Group, Version, Plural, null, false)).ConvertTo<Subscription>()!;
    }

    Task<IResource> PatchStatusAsync(Subscription subscription, string? resourceVersion, params PatchOperation[] operations) =>
        this._database.PatchSubResourceAsync(new Patch(PatchType.JsonPatch, new JsonPatch(operations)), Group, Version, Plural, subscription.GetName(), "status", null, resourceVersion, false);

    async Task<Subscription> GetAsync(Subscription subscription) => (await this._database.GetResourceAsync(Group, Version, Plural, subscription.GetName(), null))!.ConvertTo<Subscription>()!;

    [Fact]
    public async Task Concurrent_Test_And_Set_Patches_Should_Never_Both_Succeed()
    {
        // arrange: every writer increments the acked offset with a test-and-set patch, retrying whenever it loses the race
        var subscription = await this.CreateSubscriptionAsync();
        const int writers = 8, increments = 20;
        var conflicts = 0;

        // act
        await Task.WhenAll(Enumerable.Range(0, writers).Select(_ => Task.Run(async () =>
        {
            for (var i = 0; i < increments; i++)
            {
                while (true)
                {
                    var current = (long)(await this.GetAsync(subscription)).Status!.Stream!.AckedOffset!.Value;
                    try
                    {
                        await this.PatchStatusAsync(subscription, null,
                            PatchOperation.Test(JsonPointer.Parse("/status/stream/ackedOffset"), JsonValue.Create(current)),
                            PatchOperation.Replace(JsonPointer.Parse("/status/stream/ackedOffset"), JsonValue.Create(current + 1)));
                        break;
                    }
                    catch (ProblemDetailsException ex) when (ex.Problem.Status == (int)HttpStatusCode.Conflict)
                    {
                        Interlocked.Increment(ref conflicts);
                    }
                }
            }
        })));

        // assert: no increment was lost, which it would be if two writers had both passed the test against the same value
        Assert.Equal((ulong)(writers * increments), (await this.GetAsync(subscription)).Status!.Stream!.AckedOffset);
        Assert.True(conflicts > 0, "The writers never raced, so the test proves nothing");
    }

    [Fact]
    public async Task Concurrent_Patches_Of_Different_Fields_Should_All_Be_Kept()
    {
        // arrange
        var subscription = await this.CreateSubscriptionAsync();
        const int patches = 20;

        // act: patches that do not expect a version, and touch different labels, run concurrently
        await Task.WhenAll(Enumerable.Range(0, patches).Select(i => Task.Run(() => this._database.PatchResourceAsync(new Patch(PatchType.JsonPatch, new JsonPatch(
            PatchOperation.Add(JsonPointer.Parse($"/metadata/labels/label-{i}"), JsonValue.Create("value")))), Group, Version, Plural, subscription.GetName(), null, null, false))));

        // assert: each patch is applied to the state left by the previous one, so none overwrites another
        var labels = (await this.GetAsync(subscription)).Metadata.Labels!;
        Assert.All(Enumerable.Range(0, patches), i => Assert.Contains($"label-{i}", labels.Keys));
    }

    [Fact]
    public async Task A_Patch_Expecting_An_Outdated_Version_Should_Conflict_And_Change_Nothing()
    {
        // arrange
        var subscription = await this.CreateSubscriptionAsync();
        var outdatedVersion = subscription.Metadata.ResourceVersion;
        var updated = (await this.PatchStatusAsync(subscription, outdatedVersion, PatchOperation.Replace(JsonPointer.Parse("/status/stream/ackedOffset"), JsonValue.Create(1)))).ConvertTo<Subscription>()!;

        // act
        var exception = await Assert.ThrowsAsync<ProblemDetailsException>(() => this.PatchStatusAsync(subscription, outdatedVersion, PatchOperation.Replace(JsonPointer.Parse("/status/stream/ackedOffset"), JsonValue.Create(42))));

        // assert
        Assert.Equal((int)HttpStatusCode.Conflict, exception.Problem.Status);
        var current = await this.GetAsync(subscription);
        Assert.Equal(1ul, current.Status!.Stream!.AckedOffset);
        Assert.NotEqual(outdatedVersion, updated.Metadata.ResourceVersion);
        Assert.Equal(updated.Metadata.ResourceVersion, current.Metadata.ResourceVersion);
    }

    [Fact]
    public async Task A_Patch_Expecting_The_Current_Version_Should_Apply()
    {
        // arrange
        var subscription = await this.CreateSubscriptionAsync();

        // act
        var updated = (await this.PatchStatusAsync(subscription, subscription.Metadata.ResourceVersion, PatchOperation.Replace(JsonPointer.Parse("/status/stream/ackedOffset"), JsonValue.Create(7)))).ConvertTo<Subscription>()!;

        // assert
        Assert.Equal(7ul, updated.Status!.Stream!.AckedOffset);
        Assert.Equal(7ul, (await this.GetAsync(subscription)).Status!.Stream!.AckedOffset);
    }

    [Fact]
    public async Task A_Patch_Whose_Test_Fails_Should_Conflict_And_Change_Nothing()
    {
        // arrange
        var subscription = await this.CreateSubscriptionAsync();

        // act: the base implementation reported this as '304 Not Modified', which callers could not tell apart from a no-op
        var exception = await Assert.ThrowsAsync<ProblemDetailsException>(() => this.PatchStatusAsync(subscription, null,
            PatchOperation.Test(JsonPointer.Parse("/status/stream/ackedOffset"), JsonValue.Create(99)),
            PatchOperation.Replace(JsonPointer.Parse("/status/phase"), JsonValue.Create("inactive"))));

        // assert
        Assert.Equal((int)HttpStatusCode.Conflict, exception.Problem.Status);
        Assert.Equal(AtomicRedisDatabase.PatchConflictProblemType, exception.Problem.Type);
        Assert.Equal(SubscriptionStatusPhase.Active, (await this.GetAsync(subscription)).Status!.Phase);
    }

    [Fact]
    public async Task A_Status_Patch_Should_Only_Change_The_Status()
    {
        // arrange
        var subscription = await this.CreateSubscriptionAsync();

        // act
        await this.PatchStatusAsync(subscription, null,
            PatchOperation.Replace(JsonPointer.Parse("/status/phase"), JsonValue.Create("inactive")),
            PatchOperation.Replace(JsonPointer.Parse("/spec/subscriber/uri"), JsonValue.Create("http://elsewhere/events")));

        // assert
        var current = await this.GetAsync(subscription);
        Assert.Equal(SubscriptionStatusPhase.Inactive, current.Status!.Phase);
        Assert.Equal(new Uri("http://localhost/events"), current.Spec.Subscriber.Uri);
        Assert.Equal(subscription.Metadata.Generation, current.Metadata.Generation);
    }

    [Fact]
    public async Task An_Update_Should_Be_Published_To_Watchers()
    {
        // arrange
        var subscription = await this.CreateSubscriptionAsync();
        var received = new TaskCompletionSource<ResourceWatchEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        // the channel the base implementation subscribes to, to relay the events to the watches of every process
        await this._connection.GetSubscriber().SubscribeAsync(new RedisChannel("resource-watch-events", RedisChannel.PatternMode.Literal), (_, json) =>
        {
            var e = JsonSerializer.Default.Deserialize<ResourceWatchEvent>(json.ToString())!;
            if (e.Type == ResourceWatchEventType.Updated && e.Resource.GetName() == subscription.GetName()) received.TrySetResult(e);
        });

        // act
        var updated = await this.PatchStatusAsync(subscription, null, PatchOperation.Replace(JsonPointer.Parse("/status/stream/ackedOffset"), JsonValue.Create(3)));

        // assert
        var e = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(updated.Metadata.ResourceVersion, e.Resource.Metadata.ResourceVersion);
    }

}
