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
using Neuroglia.Data.Infrastructure.ResourceOriented;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace CloudStreams.IntegrationTests.Cases;

/// <summary>
/// Verifies that namespaces isolate resources and cloud events: the gateway and the broker of the fixture serve the default namespace only (see #215)
/// </summary>
[Collection(CloudStreamsCollection.Name)]
public sealed class NamespaceTests(CloudStreamsFixture fixture, ITestOutputHelper output)
    : IAsyncLifetime
{

    static Uri Source([CallerMemberName] string test = "") => new($"https://e2e.cloud-streams.io/{test.ToLowerInvariant()}");

    static string Name([CallerMemberName] string test = "") => test.ToLowerInvariant().Replace('_', '-');

    IReadOnlyList<int>? _logs;

    public Task InitializeAsync()
    {
        this._logs = fixture.CheckpointLogs();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        output.WriteLine(fixture.GetLogs(this._logs));
        return Task.CompletedTask;
    }

    async Task<Namespace> CreateNamespaceAsync(string name) => await fixture.Api.Resources.Namespaces.CreateAsync(new Namespace() { Metadata = new() { Name = name } });

    Task<Subscription> SubscribeAsync(string name, string @namespace, Uri source, TestSubscriber.Endpoint endpoint) => fixture.Api.Resources.Subscriptions.CreateAsync(new Subscription()
    {
        Metadata = new() { Name = name, Namespace = @namespace },
        Spec = new() { Partition = new(CloudEventPartitionType.BySource, source.OriginalString), Subscriber = new() { Endpoint = endpoint.Uri }, Stream = new() { Offset = 0 } }
    });

    async Task PublishAsync(Uri source, int count)
    {
        for (var i = 0; i < count; i++)
        {
            var json = $"{{\"specversion\":\"1.0\",\"id\":\"event-{i}\",\"source\":\"{source}\",\"type\":\"io.cloud-streams.e2e.v1\"}}";
            using var response = await fixture.Gateway.PostAsync("api/events/pub", new StringContent(json, Encoding.UTF8, "application/cloudevents+json"));
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }
    }

    [Fact]
    public async Task A_Namespace_Should_Only_Be_Deleted_Once_It_Holds_No_Resource()
    {
        // arrange
        var @namespace = await CreateNamespaceAsync(Name());
        var endpoint = fixture.Subscriber.Register(Name());
        await SubscribeAsync("orders", @namespace.GetName(), Source(), endpoint);

        // act
        var refused = await Assert.ThrowsAsync<ProblemDetailsException>(() => fixture.Api.Resources.Namespaces.DeleteAsync(@namespace.GetName()));
        await fixture.Api.Resources.Subscriptions.DeleteAsync("orders", @namespace.GetName());
        await fixture.Api.Resources.Namespaces.DeleteAsync(@namespace.GetName());

        // assert
        Assert.Equal((int)HttpStatusCode.Conflict, refused.Problem.Status);
        Assert.DoesNotContain(await (await fixture.Api.Resources.Namespaces.ListAsync()).ToListAsync(), n => n.GetName() == @namespace.GetName());
    }

    [Fact]
    public async Task Namespaces_Should_Be_Named_After_A_Dns_Label()
    {
        var exception = await Assert.ThrowsAsync<ProblemDetailsException>(() => CreateNamespaceAsync("Not_A_Label"));
        Assert.Equal((int)HttpStatusCode.BadRequest, exception.Problem.Status);
    }

    [Fact]
    public async Task The_Default_Namespace_Should_Not_Be_Deleted()
    {
        var exception = await Assert.ThrowsAsync<ProblemDetailsException>(() => fixture.Api.Resources.Namespaces.DeleteAsync(Namespace.DefaultNamespaceName));
        Assert.Equal((int)HttpStatusCode.Conflict, exception.Problem.Status);
    }

    [Fact]
    public async Task Events_And_Subscriptions_Should_Stay_Within_Their_Namespace()
    {
        // arrange - the same subscription in the default namespace, which the fixture's broker serves, and in another one, which no broker serves
        var other = await CreateNamespaceAsync(Name());
        var inDefault = fixture.Subscriber.Register($"{Name()}-default");
        var inOther = fixture.Subscriber.Register($"{Name()}-other");
        await SubscribeAsync("orders", Namespace.DefaultNamespaceName, Source(), inDefault);
        await SubscribeAsync("orders", other.GetName(), Source(), inOther);

        // act - the fixture's gateway serves the default namespace, which the events it ingests belong to
        await PublishAsync(Source(), 3);

        // assert - the broker of the default namespace delivers them, and only to the subscription of its own namespace
        Assert.Equal(3, (await inDefault.WaitForAsync(3)).Count);
        await Task.Delay(TimeSpan.FromSeconds(3));
        Assert.Empty(inOther.Deliveries);
        var partitions = await fixture.Api.CloudEvents.Partitions.ListPartitionsByTypeAsync(CloudEventPartitionType.BySource, @namespace: other.GetName());
        Assert.DoesNotContain(Source().OriginalString, partitions.Items);
        Assert.Contains(Source().OriginalString, (await fixture.Api.CloudEvents.Partitions.ListPartitionsByTypeAsync(CloudEventPartitionType.BySource, @namespace: Namespace.DefaultNamespaceName)).Items);
        var metadata = await fixture.Api.CloudEvents.Partitions.ListPartitionMetadataByTypeAsync(CloudEventPartitionType.BySource, Source().OriginalString, @namespace: other.GetName());
        Assert.DoesNotContain(metadata.Items, m => m.Id == Source().OriginalString);
        var subscription = await fixture.Api.Resources.Subscriptions.GetAsync("orders", other.GetName());
        Assert.Null(subscription.Status?.Stream?.AckedOffset);

        // cleanup
        await fixture.Api.Resources.Subscriptions.DeleteAsync("orders", Namespace.DefaultNamespaceName);
        await fixture.Api.Resources.Subscriptions.DeleteAsync("orders", other.GetName());
        await fixture.Api.Resources.Namespaces.DeleteAsync(other.GetName());
    }

}
