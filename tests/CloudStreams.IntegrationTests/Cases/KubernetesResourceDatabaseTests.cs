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
using CloudStreams.Core.Api.Configuration;
using CloudStreams.Core.Api.Services;
using CloudStreams.Core.Resources;
using CloudStreams.Operator.Services;
using Json.Patch;
using Json.Pointer;
using k8s;
using k8s.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Neuroglia;
using Neuroglia.Data;
using Neuroglia.Data.Infrastructure.ResourceOriented;
using Neuroglia.Data.Infrastructure.ResourceOriented.Services;
using Neuroglia.Data.PatchModel.Services;
using Neuroglia.Serialization.Json;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Nodes;
using StackExchange.Redis;
using Testcontainers.K3s;
using Testcontainers.Redis;
using Xunit;

namespace CloudStreams.IntegrationTests.Cases;

/// <summary>
/// Verifies, against a real Kubernetes API server, that the Kubernetes store of resources behaves as the Redis one does for Cloud Streams (see #275, #276, #277)
/// </summary>
public sealed class KubernetesResourceDatabaseTests(KubernetesResourceDatabaseTests.Cluster cluster)
    : IClassFixture<KubernetesResourceDatabaseTests.Cluster>
{

    const string KubernetesDefaultNamespace = "cloud-streams";

    /// <summary>
    /// A Kubernetes cluster, started once for the tests, with the CRDs of the chart installed, as an administrator installs them
    /// </summary>
    public sealed class Cluster
        : IAsyncLifetime
    {

        readonly K3sContainer _k3s = new K3sBuilder("rancher/k3s:v1.31.5-k3s1").Build();

        public IKubernetes Kubernetes { get; private set; } = null!;

        public KubernetesResourceDatabase Database { get; private set; } = null!;

        public async Task InitializeAsync()
        {
            await this._k3s.StartAsync();
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(await this._k3s.GetKubeconfigAsync()));
            this.Kubernetes = new k8s.Kubernetes(await KubernetesClientConfiguration.BuildConfigFromConfigFileAsync(stream));
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (!File.Exists(Path.Combine(directory!.FullName, "CloudStreams.sln"))) directory = directory.Parent;
            foreach (var file in Directory.GetFiles(Path.Combine(directory.FullName, "deployments", "helm", "cloud-streams", "crds"), "*.yaml"))
                await this.Kubernetes.ApiextensionsV1.CreateCustomResourceDefinitionAsync(KubernetesYaml.Deserialize<V1CustomResourceDefinition>(await File.ReadAllTextAsync(file)));
            var deadline = DateTime.UtcNow.AddSeconds(60);
            while ((await this.Kubernetes.ApiextensionsV1.ListCustomResourceDefinitionAsync()).Items.Count(c => c.Spec.Group == CloudStreamsDefaults.ResourceGroup && c.Status?.Conditions?.Any(s => s.Type == "Established" && s.Status == "True") == true) < 6)
            {
                if (DateTime.UtcNow > deadline) throw new TimeoutException("The CustomResourceDefinitions were not established");
                await Task.Delay(250);
            }
            // an established CRD may still answer '429 Too Many Requests' while its storage initializes
            foreach (var definition in CloudStreamsDefaults.Resources.Definitions.AsEnumerable())
            {
                while (true)
                {
                    try
                    {
                        await this.Kubernetes.CustomObjects.ListClusterCustomObjectAsync(definition.Spec.Group, definition.Spec.Versions.First().Name, definition.Spec.Names.Plural, limit: 1);
                        break;
                    }
                    catch (k8s.Autorest.HttpOperationException ex) when ((int)ex.Response.StatusCode == 429 && DateTime.UtcNow < deadline) { await Task.Delay(250); }
                }
            }
            await this.Kubernetes.CoreV1.CreateNamespaceAsync(new V1Namespace() { Metadata = new() { Name = KubernetesDefaultNamespace } });
            this.Database = new KubernetesResourceDatabase(NullLogger<KubernetesDatabase>.Instance, this.Kubernetes, JsonSerializer.Default, [new JsonPatchHandler(), new JsonMergePatchHandler()],
                new KubernetesResourceStoreOptions() { DefaultNamespace = KubernetesDefaultNamespace, PageSize = 2 });
            await this.Database.InitializeAsync();
            await this.Database.CreateNamespaceAsync(Namespace.DefaultNamespaceName);
        }

        public async Task DisposeAsync()
        {
            if (this.Database != null) await this.Database.DisposeAsync();
            this.Kubernetes?.Dispose();
            await this._k3s.DisposeAsync();
        }

    }

    IKubernetes _kubernetes => cluster.Kubernetes;

    KubernetesResourceDatabase _database => cluster.Database;

    static string Group => Subscription.ResourceDefinition.Group;

    static string Version => Subscription.ResourceDefinition.Version;

    static string Plural => Subscription.ResourceDefinition.Plural;

    static Subscription NewSubscription(string name, string @namespace = "default", IDictionary<string, string>? labels = null) =>
        new(new() { Name = name, Namespace = @namespace, Labels = labels }, new() { Subscriber = new() { Endpoint = new Uri("https://subscriber.example.com/events") }, Stream = new() { Offset = 0 } });

    async Task<Subscription> CreateAsync([CallerMemberName] string name = "", string @namespace = "default", IDictionary<string, string>? labels = null) =>
        (await this._database.CreateResourceAsync(NewSubscription(name.ToLowerInvariant().Replace('_', '-'), @namespace, labels), Group, Version, Plural, @namespace)).ConvertTo<Subscription>()!;

    async Task<Subscription?> GetAsync(Subscription subscription) => (await this._database.GetResourceAsync(Group, Version, Plural, subscription.GetName(), subscription.GetNamespace()))?.ConvertTo<Subscription>();

    [Fact]
    public async Task Resources_Should_Be_Created_Read_And_Deleted_In_The_Kubernetes_Namespace_Of_Their_Namespace()
    {
        // act
        var created = await this.CreateAsync();
        var read = await this.GetAsync(created);
        var stored = await this._kubernetes.CustomObjects.GetNamespacedCustomObjectAsync(Group, Version, KubernetesDefaultNamespace, Plural, created.GetName());
        var duplicate = await Assert.ThrowsAsync<ProblemDetailsException>(() => this.CreateAsync());
        await this._database.DeleteResourceAsync(Group, Version, Plural, created.GetName(), Namespace.DefaultNamespaceName);
        var missing = await Assert.ThrowsAsync<ProblemDetailsException>(() => this._database.DeleteResourceAsync(Group, Version, Plural, created.GetName(), Namespace.DefaultNamespaceName));

        // assert - the default namespace of Cloud Streams is the configured Kubernetes namespace, and is reported as 'default'
        Assert.Equal(Namespace.DefaultNamespaceName, created.GetNamespace());
        Assert.NotNull(read);
        Assert.Equal("https://subscriber.example.com/events", read.Spec.Subscriber.Endpoint!.Uri.OriginalString);
        Assert.NotNull(stored);
        // as the Redis database reports it
        Assert.True(duplicate.Problem.IsConflict());
        Assert.Equal((int)HttpStatusCode.NotFound, missing.Problem.Status);
        Assert.Null(await this.GetAsync(created));
    }

    [Fact]
    public async Task The_Status_Should_Be_Patched_And_Replaced_As_A_Sub_Resource()
    {
        // arrange
        var subscription = await this.CreateAsync();
        var patch = new Patch(PatchType.JsonPatch, new JsonPatch(PatchOperation.Add(JsonPointer.Parse("/status"), JsonNode.Parse("""{ "phase": "active", "stream": { "ackedOffset": 3 } }"""))));

        // act
        await this._database.PatchSubResourceAsync(patch, Group, Version, Plural, subscription.GetName(), "status", subscription.GetNamespace());
        var patched = (await this.GetAsync(subscription))!;
        patched.Status!.Stream!.AckedOffset = 5;
        await this._database.ReplaceSubResourceAsync(patched, Group, Version, Plural, subscription.GetName(), "status", subscription.GetNamespace());
        var replaced = (await this.GetAsync(subscription))!;
        var unsupported = await Assert.ThrowsAsync<ProblemDetailsException>(() => this._database.PatchSubResourceAsync(patch, Group, Version, Plural, subscription.GetName(), "scale", subscription.GetNamespace()));

        // assert
        Assert.Equal(SubscriptionStatusPhase.Active, patched.Status.Phase);
        Assert.Equal(5ul, replaced.Status!.Stream!.AckedOffset);
        Assert.Equal(subscription.Spec.Subscriber.Endpoint, replaced.Spec.Subscriber.Endpoint);
        Assert.NotNull(unsupported);
    }

    [Fact]
    public async Task Updates_Should_Be_Checked_Against_The_Expected_Version()
    {
        // arrange
        var subscription = await this.CreateAsync();
        var suspend = new Patch(PatchType.JsonPatch, new JsonPatch(PatchOperation.Add(JsonPointer.Parse("/spec/suspended"), JsonValue.Create(true))));

        // act - a patch that expects the current version is applied, and then that version is stale
        await this._database.PatchResourceAsync(suspend, Group, Version, Plural, subscription.GetName(), subscription.GetNamespace(), subscription.Metadata.ResourceVersion);
        var stalePatch = await Assert.ThrowsAsync<ProblemDetailsException>(() => this._database.PatchResourceAsync(suspend, Group, Version, Plural, subscription.GetName(), subscription.GetNamespace(), subscription.Metadata.ResourceVersion));
        var staleReplacement = await Assert.ThrowsAsync<ProblemDetailsException>(() => this._database.ReplaceResourceAsync(subscription, Group, Version, Plural, subscription.GetName(), subscription.GetNamespace()));

        // assert
        Assert.True((await this.GetAsync(subscription))!.Spec.Suspended);
        Assert.Equal((int)HttpStatusCode.Conflict, stalePatch.Problem.Status);
        Assert.Equal((int)HttpStatusCode.Conflict, staleReplacement.Problem.Status);
    }

    [Fact]
    public async Task Concurrent_Test_And_Set_Patches_Should_Never_Both_Succeed()
    {
        // arrange - every writer increments the acked offset with a test-and-set patch, as brokers acknowledge offsets
        var subscription = await this.CreateAsync();
        await this._database.PatchSubResourceAsync(new Patch(PatchType.JsonPatch, new JsonPatch(PatchOperation.Add(JsonPointer.Parse("/status"), JsonNode.Parse("""{ "stream": { "ackedOffset": 0 } }""")))),
            Group, Version, Plural, subscription.GetName(), "status", subscription.GetNamespace());
        const int writers = 4, increments = 5;

        // act
        await Task.WhenAll(Enumerable.Range(0, writers).Select(_ => Task.Run(async () =>
        {
            for (var i = 0; i < increments; i++)
            {
                while (true)
                {
                    var current = (long)(await this.GetAsync(subscription))!.Status!.Stream!.AckedOffset!.Value;
                    try
                    {
                        await this._database.PatchSubResourceAsync(new Patch(PatchType.JsonPatch, new JsonPatch(
                            PatchOperation.Test(JsonPointer.Parse("/status/stream/ackedOffset"), JsonValue.Create(current)),
                            PatchOperation.Replace(JsonPointer.Parse("/status/stream/ackedOffset"), JsonValue.Create(current + 1)))), Group, Version, Plural, subscription.GetName(), "status", subscription.GetNamespace());
                        break;
                    }
                    catch (ProblemDetailsException ex) when (ex.Problem.Status == (int)HttpStatusCode.Conflict) { }
                }
            }
        })));

        // assert - no increment was lost
        Assert.Equal((ulong)(writers * increments), (await this.GetAsync(subscription))!.Status!.Stream!.AckedOffset);
    }

    [Fact]
    public async Task Lists_Should_Be_Paged_And_Filtered_By_Labels()
    {
        // arrange
        var labels = new Dictionary<string, string>() { ["team"] = "billing" };
        for (var i = 0; i < 3; i++) await this.CreateAsync($"paged-{i}", labels: labels);
        await this.CreateAsync("unlabelled");

        // act - pages hold 2 resources
        var all = await this._database.GetResourcesAsync(Group, Version, Plural, Namespace.DefaultNamespaceName, [new LabelSelector("team", LabelSelectionOperator.Equals, "billing")]).ToListAsync();
        var firstPage = await this._database.ListResourcesAsync(Group, Version, Plural, Namespace.DefaultNamespaceName, [new LabelSelector("team", LabelSelectionOperator.Equals, "billing")], 2);

        // assert
        Assert.Equal(["paged-0", "paged-1", "paged-2"], all.Select(r => r.GetName()).Order());
        Assert.Equal(2, firstPage.Items!.Count);
        Assert.False(string.IsNullOrWhiteSpace(firstPage.Metadata.Continue));
    }

    [Fact]
    public async Task Watches_Should_Emit_The_Changes_Of_The_Selected_Resources_Only()
    {
        // arrange
        var events = new List<IResourceWatchEvent>();
        await using var watch = await this._database.WatchResourcesAsync(Group, Version, Plural, Namespace.DefaultNamespaceName, [new LabelSelector("watched", LabelSelectionOperator.Equals, "true")]);
        using var subscription = watch.Subscribe(e => { lock (events) events.Add(e); });

        // act
        var watched = await this.CreateAsync("watched", labels: new Dictionary<string, string>() { ["watched"] = "true" });
        await this.CreateAsync("ignored");
        await this._database.PatchResourceAsync(new Patch(PatchType.JsonPatch, new JsonPatch(PatchOperation.Add(JsonPointer.Parse("/spec/suspended"), JsonValue.Create(true)))), Group, Version, Plural, watched.GetName(), watched.GetNamespace());
        await this._database.DeleteResourceAsync(Group, Version, Plural, watched.GetName(), watched.GetNamespace());
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (DateTime.UtcNow < deadline) { lock (events) if (events.Count >= 3) break; await Task.Delay(100); }

        // assert
        lock (events)
        {
            Assert.Equal([ResourceWatchEventType.Created, ResourceWatchEventType.Updated, ResourceWatchEventType.Deleted], events.Select(e => e.Type));
            Assert.All(events, e => Assert.Equal(("watched", Namespace.DefaultNamespaceName), (e.Resource.GetName(), e.Resource.GetNamespace())));
        }
    }

    /// <summary>
    /// The API server closes watches every few minutes: a broker that stopped seeing the changes of its subscriptions would silently stop running them
    /// </summary>
    [Fact]
    public async Task Watches_Should_Resume_When_The_API_Server_Closes_Them()
    {
        // arrange - the API server closes the watch after a second
        await using var database = new KubernetesResourceDatabase(NullLogger<KubernetesDatabase>.Instance, this._kubernetes, JsonSerializer.Default, [new JsonPatchHandler(), new JsonMergePatchHandler()],
            new KubernetesResourceStoreOptions() { DefaultNamespace = KubernetesDefaultNamespace, WatchTimeout = TimeSpan.FromSeconds(1) });
        var events = new List<IResourceWatchEvent>();
        await using var watch = await database.WatchResourcesAsync(Group, Version, Plural, Namespace.DefaultNamespaceName, [new LabelSelector("resumed", LabelSelectionOperator.Equals, "true")]);
        using var subscription = watch.Subscribe(e => { lock (events) events.Add(e); });

        // act - the change is made once the watch has been closed and resumed several times
        await Task.Delay(TimeSpan.FromSeconds(4));
        await this.CreateAsync("resumed", labels: new Dictionary<string, string>() { ["resumed"] = "true" });
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (DateTime.UtcNow < deadline) { lock (events) if (events.Count >= 1) break; await Task.Delay(100); }
        await Task.Delay(TimeSpan.FromSeconds(3));

        // assert - the change is emitted once: resuming neither misses nor replays it
        lock (events) Assert.Equal([(ResourceWatchEventType.Created, "resumed")], events.Select(e => (e.Type, e.Resource.GetName())));
    }

    [Fact]
    public async Task Namespaces_Should_Be_Labelled_Kubernetes_Namespaces()
    {
        // act
        await this._database.CreateNamespaceAsync("tenant-a");
        var kubernetesNamespace = await this._kubernetes.CoreV1.ReadNamespaceAsync("tenant-a");
        var listed = await this._database.GetResourcesAsync(NamespaceDefinition.ResourceGroup, NamespaceDefinition.ResourceVersion, NamespaceDefinition.ResourcePlural).Select(n => n.GetName()).ToListAsync();
        await this.CreateAsync("in-tenant-a", "tenant-a");
        await this._database.DeleteResourceAsync(NamespaceDefinition.ResourceGroup, NamespaceDefinition.ResourceVersion, NamespaceDefinition.ResourcePlural, "tenant-a");
        var kept = await this._kubernetes.CoreV1.ReadNamespaceAsync("tenant-a");

        // assert - the default namespace of Cloud Streams is the configured Kubernetes namespace, and removing a namespace only removes its label
        Assert.Equal("true", kubernetesNamespace.Metadata.Labels["cloud-streams.io/enabled"]);
        Assert.Contains(Namespace.DefaultNamespaceName, listed);
        Assert.Contains("tenant-a", listed);
        Assert.DoesNotContain(KubernetesDefaultNamespace, listed);
        Assert.DoesNotContain("kube-system", listed);
        Assert.False(kept.Metadata.Labels?.ContainsKey("cloud-streams.io/enabled") == true);
        Assert.Null(await this._database.GetResourceAsync<Namespace>("tenant-a"));
    }

    /// <summary>
    /// Creates the namespaces 'served-a', 'served-b' and 'unserved-c', and a database that only serves the first two, as a gateway restricted to them does
    /// </summary>
    async Task<KubernetesResourceDatabase> CreateRestrictedDatabaseAsync()
    {
        foreach (var name in new[] { "served-a", "served-b", "unserved-c" })
        {
            try { await this._database.CreateNamespaceAsync(name); }
            catch (ProblemDetailsException ex) when (ex.Problem.IsConflict()) { }
        }
        var database = new KubernetesResourceDatabase(NullLogger<KubernetesDatabase>.Instance, this._kubernetes, JsonSerializer.Default, [new JsonPatchHandler(), new JsonMergePatchHandler()],
            new KubernetesResourceStoreOptions() { DefaultNamespace = KubernetesDefaultNamespace, PageSize = 2, Namespaces = ["served-a", "served-b"] });
        await database.InitializeAsync();
        return database;
    }

    [Fact]
    public async Task Restricted_Processes_Should_Only_List_And_Access_Their_Namespaces()
    {
        // arrange
        await using var database = await this.CreateRestrictedDatabaseAsync();
        var labels = new Dictionary<string, string>() { ["restricted"] = "true" };
        await this.CreateAsync("listed-a1", "served-a", labels);
        await this.CreateAsync("listed-a2", "served-a", labels);
        await this.CreateAsync("listed-b1", "served-b", labels);
        var unserved = await this.CreateAsync("hidden-c1", "unserved-c", labels);
        LabelSelector[] selector = [new LabelSelector("restricted", LabelSelectionOperator.Equals, "true")];

        // act - pages hold 2 resources, and span namespaces
        var all = await database.GetResourcesAsync(Group, Version, Plural, null, selector).ToListAsync();
        var firstPage = await database.ListResourcesAsync(Group, Version, Plural, null, selector, 2);
        var secondPage = await database.ListResourcesAsync(Group, Version, Plural, null, selector, 2, firstPage.Metadata.Continue);
        var namespaces = await database.GetResourcesAsync(NamespaceDefinition.ResourceGroup, NamespaceDefinition.ResourceVersion, NamespaceDefinition.ResourcePlural).Select(n => n.GetName()).ToListAsync();
        var read = await Assert.ThrowsAsync<ProblemDetailsException>(() => database.GetResourceAsync(Group, Version, Plural, unserved.GetName(), unserved.GetNamespace()));
        var created = await Assert.ThrowsAsync<ProblemDetailsException>(() => database.CreateResourceAsync(NewSubscription("refused", "unserved-c"), Group, Version, Plural, "unserved-c"));
        var deleted = await Assert.ThrowsAsync<ProblemDetailsException>(() => database.DeleteResourceAsync(Group, Version, Plural, unserved.GetName(), unserved.GetNamespace()));
        var namespaceCreated = await Assert.ThrowsAsync<ProblemDetailsException>(() => database.CreateNamespaceAsync("unserved-d"));
        var listed = await Assert.ThrowsAsync<ProblemDetailsException>(() => database.ListResourcesAsync(Group, Version, Plural, "unserved-c"));

        // assert
        Assert.Equal(["listed-a1", "listed-a2", "listed-b1"], all.Select(r => r.GetName()).Order());
        Assert.Equal(2, firstPage.Items!.Count);
        Assert.False(string.IsNullOrWhiteSpace(firstPage.Metadata.Continue));
        Assert.Single(secondPage.Items!);
        Assert.True(string.IsNullOrWhiteSpace(secondPage.Metadata.Continue));
        Assert.Equal(["listed-a1", "listed-a2", "listed-b1"], firstPage.Items!.Concat(secondPage.Items!).OfType<IResource>().Select(r => r.GetName()).Order());
        Assert.Equal(["served-a", "served-b"], namespaces.Order());
        Assert.All([read, created, deleted, namespaceCreated, listed], ex => Assert.Equal((int)HttpStatusCode.Forbidden, ex.Problem.Status));
        Assert.Null(await database.GetResourceAsync<Namespace>("unserved-c"));
        Assert.NotNull(await this.GetAsync(unserved));
    }

    [Fact]
    public async Task Restricted_Processes_Should_Watch_The_Changes_Of_Their_Namespaces_Only()
    {
        // arrange
        await using var database = await this.CreateRestrictedDatabaseAsync();
        var events = new List<IResourceWatchEvent>();
        await using var watch = await database.WatchResourcesAsync(Group, Version, Plural, null, [new LabelSelector("merged", LabelSelectionOperator.Equals, "true")]);
        using var subscription = watch.Subscribe(e => { lock (events) events.Add(e); });
        var labels = new Dictionary<string, string>() { ["merged"] = "true" };

        // act
        await this.CreateAsync("merged-a", "served-a", labels);
        await this.CreateAsync("merged-c", "unserved-c", labels);
        await this.CreateAsync("merged-b", "served-b", labels);
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (DateTime.UtcNow < deadline) { lock (events) if (events.Count >= 2) break; await Task.Delay(100); }
        await Task.Delay(TimeSpan.FromSeconds(2));

        // assert - the changes of both served namespaces are merged, and none of the other is emitted
        lock (events)
        {
            Assert.Equal([("merged-a", "served-a"), ("merged-b", "served-b")], events.Select(e => (e.Resource.GetName(), e.Resource.GetNamespace()!)).Order());
            Assert.All(events, e => Assert.Equal(ResourceWatchEventType.Created, e.Type));
        }
    }

    [Fact]
    public async Task Definitions_Should_Be_Read_From_The_Installed_Custom_Resource_Definitions()
    {
        // act
        var definition = await this._database.GetResourceAsync(ResourceDefinition.ResourceGroup, ResourceDefinition.ResourceVersion, ResourceDefinition.ResourcePlural, CloudStreamsDefaults.Resources.Definitions.Subscription.GetName());
        var unknown = await this._database.GetResourceAsync(ResourceDefinition.ResourceGroup, ResourceDefinition.ResourceVersion, ResourceDefinition.ResourcePlural, "unknowns.cloud-streams.io");
        var readOnly = await Assert.ThrowsAsync<ProblemDetailsException>(() => this._database.CreateResourceAsync(CloudStreamsDefaults.Resources.Definitions.Subscription, ResourceDefinition.ResourceGroup, ResourceDefinition.ResourceVersion, ResourceDefinition.ResourcePlural));

        // assert - the definition of Cloud Streams is returned, whose schema is richer than the CRD's
        Assert.NotNull(definition);
        Assert.Equal(CloudStreamsDefaults.Resources.Definitions.Subscription.GetName(), definition.GetName());
        Assert.False(string.IsNullOrWhiteSpace(definition.Metadata.ResourceVersion));
        Assert.Null(unknown);
        Assert.Equal((int)HttpStatusCode.MethodNotAllowed, readOnly.Problem.Status);
    }

    [Fact]
    public async Task The_Resources_Of_Redis_Should_Be_Migrated_With_Their_Status()
    {
        // arrange: a Redis store with a namespace, a sink and subscriptions, one of which has delivered up to an offset (see #283)
        await using var container = new RedisBuilder("redis:7.4-alpine").Build();
        await container.StartAsync();
        await using var connection = await ConnectionMultiplexer.ConnectAsync(container.GetConnectionString());
        await using var redis = new AtomicRedisDatabase(NullLoggerFactory.Instance, connection, JsonSerializer.Default, [new JsonPatchHandler(), new JsonMergePatchHandler()]);
        // as the applications initialize it, with the default namespace
        await redis.InitializeAsync();
        foreach (var definition in CloudStreamsDefaults.Resources.Definitions.AsEnumerable()) await redis.CreateResourceAsync(definition);
        await redis.CreateNamespaceAsync("migrated");
        var sink = new Sink(new() { Name = "archive", Namespace = "migrated" }, new() { Endpoint = new Uri("https://archive.example.com/events") });
        await redis.CreateResourceAsync(sink, Sink.ResourceDefinition.Group, Sink.ResourceDefinition.Version, Sink.ResourceDefinition.Plural, "migrated");
        var delivered = new Subscription(new() { Name = "delivered", Namespace = "migrated", Labels = new Dictionary<string, string>() { ["team"] = "orders" } },
            new() { Subscriber = new() { Endpoint = new Uri("https://subscriber.example.com/events") }, Stream = new() { Offset = 0 } },
            new() { Phase = SubscriptionStatusPhase.Active, Stream = new() { AckedOffset = 42 } });
        await redis.CreateResourceAsync(delivered, Group, Version, Plural, "migrated");
        await redis.CreateResourceAsync(NewSubscription("of-the-default-namespace"), Group, Version, Plural, Namespace.DefaultNamespaceName);
        var migration = new RedisMigration(NullLogger<RedisMigration>.Instance, redis, this._database, JsonSerializer.Default);

        // act
        var dryRun = await migration.RunAsync(true);
        var first = await migration.RunAsync(false);
        var second = await migration.RunAsync(false);

        // assert
        var migrated = (await this._database.GetResourceAsync(Group, Version, Plural, "delivered", "migrated"))!.ConvertTo<Subscription>()!;
        Assert.Equal(42ul, migrated.Status?.Stream?.AckedOffset);
        Assert.Equal("orders", migrated.Metadata.Labels!["team"]);
        Assert.NotNull(await this._database.GetResourceAsync(Sink.ResourceDefinition.Group, Sink.ResourceDefinition.Version, Sink.ResourceDefinition.Plural, "archive", "migrated"));
        // the resources of the 'default' namespace go to the Kubernetes namespace it maps to
        Assert.NotNull(await this._kubernetes.CustomObjects.GetNamespacedCustomObjectAsync(Group, Version, KubernetesDefaultNamespace, Plural, "of-the-default-namespace"));
        var labelled = await this._kubernetes.CoreV1.ReadNamespaceAsync("migrated");
        Assert.Equal("true", labelled.Metadata.Labels["cloud-streams.io/enabled"]);
        // a dry run copies nothing, and a migration may be run again: what was copied is skipped
        Assert.Equal(first.Copied, dryRun.Copied);
        Assert.Equal(4, first.Copied);
        Assert.Equal(0, second.Copied);
        Assert.Equal(first.Copied + first.Existing, second.Existing);
    }

}
