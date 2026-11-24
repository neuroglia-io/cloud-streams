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
using CloudStreams.Core.Api.Client.Services;
using CloudStreams.Core.Resources;
using CloudStreams.Dashboard.Components.ResourceManagement;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Neuroglia;
using Neuroglia.Data.Infrastructure.ResourceOriented;
using Neuroglia.Serialization;
using System.Net;
using System.Reactive.Linq;
using System.Text;
using Xunit;

namespace CloudStreams.Dashboard.UnitTests.Cases;

/// <summary>
/// Covers the <see cref="ResourceManagementComponentStore{TState, TResource}"/>, which backs the dashboard's resource lists (#204)
/// </summary>
public sealed class ResourceManagementComponentStoreTests
    : IDisposable
{

    readonly List<HttpRequestMessage> _requests = [];
    readonly TestStore _store;
    Func<HttpRequestMessage, HttpResponseMessage> _respond = _ => new(HttpStatusCode.OK) { Content = new StringContent("[]", Encoding.UTF8, "application/json") };

    public ResourceManagementComponentStoreTests()
    {
        var handler = new DelegatingStubHandler(request => { this._requests.Add(request); return this._respond(request); });
        var http = new HttpClient(handler) { BaseAddress = new("http://localhost/") };
        var services = new ServiceCollection().AddLogging().AddSingleton<IJsonSerializer>(Neuroglia.Serialization.Json.JsonSerializer.Default).BuildServiceProvider();
        var client = new CloudStreamsCoreApiClient(services, NullLoggerFactory.Instance, Neuroglia.Serialization.Json.JsonSerializer.Default, http);
        var hub = new ResourceWatchEventHubClient(new HubConnectionBuilder().WithUrl("http://localhost/api/ws/resources/watch").Build());
        this._store = new TestStore(client, hub);
    }

    public void Dispose() => this._store.Dispose();

    static Subscription Subscription(string name, IDictionary<string, string>? labels = null) => new()
    {
        Metadata = new() { Name = name, Labels = labels == null ? null : new Dictionary<string, string>(labels) },
        Spec = new() { Subscriber = new() { Uri = new($"https://{name}.example.com/events") } }
    };

    static string Json(object value) => Neuroglia.Serialization.Json.JsonSerializer.Default.SerializeToText(value);

    // ---------- filters and selection ----------

    [Fact]
    public void Adding_A_Label_Selector_Should_Replace_The_One_With_The_Same_Key()
    {
        // act
        this._store.AddLabelSelector(new("tier", LabelSelectionOperator.Equals, "edge"));
        this._store.AddLabelSelector(new("team", LabelSelectionOperator.Equals, "orders"));
        this._store.AddLabelSelector(new("tier", LabelSelectionOperator.Equals, "core"));
        this._store.RemoveLabelSelector("team");

        // assert
        var selector = Assert.Single(this._store.State.LabelSelectors!);
        Assert.Equal("tier", selector.Key);
        Assert.Equal("core", selector.Value);
    }

    [Fact]
    public async Task Toggling_The_Selection_Should_Select_All_Then_None_And_Toggle_Single_Resources()
    {
        // arrange
        await this._store.RaiseAsync(ResourceWatchEventType.Created, Subscription("a"));
        await this._store.RaiseAsync(ResourceWatchEventType.Created, Subscription("b"));

        // act & assert
        this._store.ToggleResourceSelection();
        Assert.Equal(["a", "b"], this._store.State.SelectedResourceNames);
        this._store.ToggleResourceSelection();
        Assert.Empty(this._store.State.SelectedResourceNames);
        this._store.ToggleResourceSelection("b");
        Assert.Equal(["b"], this._store.State.SelectedResourceNames);
        this._store.ToggleResourceSelection("b");
        Assert.Empty(this._store.State.SelectedResourceNames);
    }

    [Fact]
    public async Task The_Listed_Resources_Should_Be_Filtered_By_The_Search_Term()
    {
        // arrange
        await this._store.RaiseAsync(ResourceWatchEventType.Created, Subscription("orders-created"));
        await this._store.RaiseAsync(ResourceWatchEventType.Created, Subscription("billing"));

        // act
        this._store.SetSearchTerm("orders");
        var resources = await this._store.Resources.Where(r => r?.Count == 1).FirstAsync().Timeout(TimeSpan.FromSeconds(5));

        // assert
        Assert.Equal("orders-created", Assert.Single(resources!).GetName());
    }

    // ---------- real-time updates ----------

    [Fact]
    public async Task Watch_Events_Should_Add_Each_Resource_Once_Update_It_In_Place_And_Remove_It()
    {
        // act & assert
        await this._store.RaiseAsync(ResourceWatchEventType.Created, Subscription("a"));
        await this._store.RaiseAsync(ResourceWatchEventType.Created, Subscription("b"));
        await this._store.RaiseAsync(ResourceWatchEventType.Created, Subscription("a"));
        Assert.Equal(["a", "b"], this._store.State.Resources!.Select(r => r.GetName()));

        var updated = Subscription("a");
        updated.Spec.Subscriber.RateLimit = 5;
        await this._store.RaiseAsync(ResourceWatchEventType.Updated, updated);
        Assert.Equal(["a", "b"], this._store.State.Resources!.Select(r => r.GetName()));
        Assert.Equal(5, this._store.State.Resources![0].Spec.Subscriber.RateLimit);

        await this._store.RaiseAsync(ResourceWatchEventType.Deleted, Subscription("a"));
        Assert.Equal(["b"], this._store.State.Resources!.Select(r => r.GetName()));
    }

    [Fact]
    public async Task Watch_Events_For_Resources_Outside_The_Label_Selection_Should_Be_Ignored()
    {
        // arrange
        this._store.AddLabelSelector(new("tier", LabelSelectionOperator.Equals, "edge"));

        // act
        await this._store.RaiseAsync(ResourceWatchEventType.Created, Subscription("core", new Dictionary<string, string>() { ["tier"] = "core" }));
        await this._store.RaiseAsync(ResourceWatchEventType.Created, Subscription("unlabelled"));
        await this._store.RaiseAsync(ResourceWatchEventType.Created, Subscription("edge", new Dictionary<string, string>() { ["tier"] = "edge" }));

        // assert
        Assert.Equal("edge", Assert.Single(this._store.State.Resources!).GetName());
    }

    // ---------- API calls ----------

    [Fact]
    public async Task Listing_Resources_Should_Send_The_Label_Selectors_And_Store_The_Result()
    {
        // arrange
        this._respond = _ => new(HttpStatusCode.OK) { Content = new StringContent(Json(new[] { Subscription("a"), Subscription("b") }), Encoding.UTF8, "application/json") };

        // act
        await this._store.ListResourcesAsync(new() { LabelSelectors = [new("tier", LabelSelectionOperator.Equals, "edge")] });

        // assert
        Assert.Equal("?labelSelector=tier%3Dedge", Assert.Single(this._requests).RequestUri!.Query);
        Assert.Equal(["a", "b"], this._store.State.Resources!.Select(r => r.GetName()));
        Assert.False(this._store.State.Loading);
    }

    [Fact]
    public async Task A_Failure_To_List_Resources_Should_Stop_Loading()
    {
        // arrange
        this._respond = _ => new(HttpStatusCode.InternalServerError);

        // act
        await this._store.ListResourcesAsync();

        // assert
        Assert.False(this._store.State.Loading);
    }

    [Fact]
    public async Task Deleting_A_Resource_Should_Remove_Only_That_Resource()
    {
        // arrange: resources are cluster-scoped, so they all share the same (empty) namespace (see #190)
        await this._store.RaiseAsync(ResourceWatchEventType.Created, Subscription("a"));
        await this._store.RaiseAsync(ResourceWatchEventType.Created, Subscription("b"));
        this._respond = _ => new(HttpStatusCode.OK) { Content = new StringContent(Json(Subscription("a")), Encoding.UTF8, "application/json") };

        // act
        await this._store.DeleteResourceAsync(this._store.State.Resources![0]);

        // assert
        var request = Assert.Single(this._requests);
        Assert.Equal(HttpMethod.Delete, request.Method);
        Assert.EndsWith("/subscriptions/a", request.RequestUri!.AbsolutePath);
        Assert.Equal(["b"], this._store.State.Resources!.Select(r => r.GetName()));
    }

    [Fact]
    public async Task Deleting_The_Selected_Resources_Should_Delete_Each_Of_Them_And_Clear_The_Selection()
    {
        // arrange
        foreach (var name in new[] { "a", "b", "c" }) await this._store.RaiseAsync(ResourceWatchEventType.Created, Subscription(name));
        this._store.ToggleResourceSelection("a");
        this._store.ToggleResourceSelection("c");
        this._respond = request => new(HttpStatusCode.OK) { Content = new StringContent(Json(Subscription(request.RequestUri!.Segments[^1])), Encoding.UTF8, "application/json") };

        // act
        await this._store.DeleteSelectedResourcesAsync();

        // assert
        Assert.Equal(["/api/resources/v1/subscriptions/a", "/api/resources/v1/subscriptions/c"], this._requests.Select(r => r.RequestUri!.AbsolutePath));
        Assert.Equal(["b"], this._store.State.Resources!.Select(r => r.GetName()));
        Assert.Empty(this._store.State.SelectedResourceNames);
    }

    sealed class TestStore(ICloudStreamsCoreApiClient client, ResourceWatchEventHubClient hub)
        : ResourceManagementComponentStore<ResourceManagementComponentState<Subscription>, Subscription>(NullLogger<ResourceManagementComponentStore<ResourceManagementComponentState<Subscription>, Subscription>>.Instance, client, hub)
    {

        public ResourceManagementComponentState<Subscription> State => this.Get();

        public Task RaiseAsync(ResourceWatchEventType type, Subscription resource) => this.OnResourceWatchEventAsync(new ResourceWatchEvent<Subscription>(type, resource));

    }

    sealed class DelegatingStubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        : HttpMessageHandler
    {

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(respond(request));

    }

}
