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
using CloudStreams.Core.Resources;
using Neuroglia;
using CloudStreams.Core.Api.Services;
using CloudStreams.Core.Application.Services;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.DependencyInjection;
using Neuroglia.Data.Infrastructure.EventSourcing;
using Neuroglia.Serialization;
using System.Net;
using Xunit;

namespace CloudStreams.IntegrationTests.Cases;

/// <summary>
/// Verifies the retention policy of the cloud event stream against EventStoreDB (see #206). The policy applies to the whole stream, so these tests have an
/// EventStoreDB of their own, rather than sharing the one of the other tests
/// </summary>
public sealed class RetentionTests
    : IAsyncLifetime
{

    static readonly Uri Source = new("https://retention.cloud-streams.io");

    readonly IContainer _eventStore = new ContainerBuilder()
        .WithImage("eventstore/eventstore:24.10.6")
        .WithEnvironment("EVENTSTORE_INSECURE", "true")
        .WithEnvironment("EVENTSTORE_RUN_PROJECTIONS", "All")
        .WithEnvironment("EVENTSTORE_START_STANDARD_PROJECTIONS", "true")
        .WithPortBinding(2113, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request => request.ForPort(2113).ForPath("/health/live").ForStatusCode(HttpStatusCode.NoContent)))
        .Build();
    ServiceProvider _services = null!;
    CloudEventStore _store = null!;
    IStreamRetentionStore _retention = null!;

    public async Task InitializeAsync()
    {
        using var startup = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        await this._eventStore.StartAsync(startup.Token);
        var connectionString = $"esdb://{this._eventStore.Hostname}:{this._eventStore.GetMappedPublicPort(2113)}?tls=false";
        var services = new ServiceCollection().AddLogging();
        services.AddSerialization();
        services.AddJsonSerializer();
        services.AddEventStoreClient(connectionString);
        services.AddEventStorePersistentSubscriptionsClient(connectionString);
        services.AddEventStoreProjectionManagementClient(connectionString);
        services.AddEsdbEventStore();
        services.AddEsdbProjectionManager();
        services.AddSingleton<IStreamRetentionStore, EsdbStreamRetentionStore>();
        services.AddSingleton<CloudEventStore>();
        this._services = services.BuildServiceProvider();
        this._store = this._services.GetRequiredService<CloudEventStore>();
        this._retention = this._services.GetRequiredService<IStreamRetentionStore>();
        // the projection that partitions events by source may take a moment to be accepted by a node that has just started
        for (var attempt = 0; (await this._store.SetupProjectionsAsync()).Count > 0; attempt++)
        {
            if (attempt == 20) throw new InvalidOperationException("Failed to set up the projections");
            await Task.Delay(500);
        }
    }

    public async Task DisposeAsync()
    {
        await this._services.DisposeAsync();
        await this._eventStore.DisposeAsync();
    }

    async Task AppendAsync(int count)
    {
        for (var i = 0; i < count; i++)
        {
            var attributes = new Dictionary<string, object>() { ["specversion"] = "1.0", ["id"] = Guid.NewGuid().ToString(), ["source"] = Source.OriginalString, ["type"] = "io.cloud-streams.retention.v1", ["time"] = DateTimeOffset.UtcNow };
            await this._store.AppendAsync(new CloudEventDescriptor(new CloudEventMetadata(attributes), new Dictionary<string, object>() { ["index"] = i }));
        }
    }

    async Task<PartitionMetadata> WaitForPartitionAsync(ulong length)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (true)
        {
            try
            {
                var metadata = await this._store.GetPartitionMetadataAsync(null, new(CloudEventPartitionType.BySource, Source.OriginalString));
                if (metadata.Length >= length) return metadata;
            }
            catch (ProblemDetailsException) when (DateTime.UtcNow < deadline) { }
            if (DateTime.UtcNow > deadline) throw new TimeoutException("The partition has not been projected in time");
            await Task.Delay(250);
        }
    }

    [Fact]
    public async Task Expired_Events_Should_Be_Skipped_By_Reads_Of_The_Stream_And_Of_Its_Partitions()
    {
        // arrange
        var partition = new PartitionReference(CloudEventPartitionType.BySource, Source.OriginalString);
        await AppendAsync(5);
        await WaitForPartitionAsync(5);

        // act
        await this._retention.SetAsync(new() { MaxCount = 2 });

        // assert - the stream
        var stream = await this._store.GetStreamMetadataAsync(null);
        Assert.Equal(5ul, stream.Length);
        Assert.Equal(3ul, stream.FirstOffset);
        Assert.Equal(2ul, stream.Retention!.MaxCount);
        Assert.Equal([3ul, 4ul], await this._store.ReadAsync(null, StreamReadDirection.Forwards, StreamPosition.StartOfStream).Select(r => r.Sequence).ToListAsync());

        // assert - the partition, whose links to expired events can no longer be read
        var metadata = await this._store.GetPartitionMetadataAsync(null, partition);
        Assert.Equal(5ul, metadata.Length);
        Assert.Equal(3ul, metadata.FirstOffset);
        Assert.Equal([3ul, 4ul], await this._store.ReadPartitionAsync(null, partition, StreamReadDirection.Forwards, StreamPosition.StartOfStream).Select(r => r.Sequence).ToListAsync());
        Assert.Equal([3ul], await this._store.ReadPartitionAsync(null, partition, StreamReadDirection.Forwards, 1, 1).Select(r => r.Sequence).ToListAsync());
        Assert.Equal([4ul, 3ul], await this._store.ReadPartitionAsync(null, partition, StreamReadDirection.Backwards, StreamPosition.EndOfStream).Select(r => r.Sequence).ToListAsync());
        Assert.Empty(await this._store.ReadPartitionAsync(null, partition, StreamReadDirection.Backwards, 2).ToListAsync());
        Assert.Equal(3ul, await this._store.FindOffsetAsync(null, DateTimeOffset.UtcNow.AddHours(-1), partition));

        // act - the policy is removed
        await this._retention.SetAsync(new());

        // assert
        Assert.Null(await this._retention.GetAsync());
    }

}
