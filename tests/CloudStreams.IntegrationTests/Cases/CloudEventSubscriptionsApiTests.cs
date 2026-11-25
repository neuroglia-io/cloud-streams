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

using Neuroglia.Eventing.CloudEvents;
using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;
using Xunit.Abstractions;

namespace CloudStreams.IntegrationTests.Cases;

/// <summary>
/// Manages subscriptions through the CloudEvents Subscriptions API, end to end (#219)
/// </summary>
[Collection(CloudStreamsCollection.Name)]
public sealed class CloudEventSubscriptionsApiTests(CloudStreamsFixture fixture, ITestOutputHelper output)
    : IAsyncLifetime
{

    const string Path = "api/cloudevents/v1/subscriptions";

    static string Source([CallerMemberName] string test = "") => $"https://e2e.cloud-streams.io/{test.ToLowerInvariant()}";

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

    async Task PublishAsync(string source, string id, string type, string subject)
    {
        var json = $"{{\"specversion\":\"1.0\",\"id\":\"{id}\",\"source\":\"{source}\",\"type\":\"{type}\",\"subject\":\"{subject}\"}}";
        using var response = await fixture.Gateway.PostAsync("api/events/pub", new StringContent(json, Encoding.UTF8, CloudEventContentType.Json));
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Subscriptions_Should_Be_Managed_Through_The_CloudEvents_Subscriptions_Api()
    {
        // arrange - push or pull request events of the source, except those about the 'spec' repository
        var endpoint = fixture.Subscriber.Register(Name());
        var submitted = new JsonObject()
        {
            ["id"] = "ignored",
            ["source"] = Source(),
            ["filters"] = new JsonArray(
                JsonNode.Parse("""{ "any": [ { "exact": { "type": "com.github.push" } }, { "prefix": { "type": "com.github.pull_request" } } ] }"""),
                JsonNode.Parse("""{ "not": { "suffix": { "subject": "/spec" } } }""")),
            ["sink"] = endpoint.Uri.OriginalString,
            ["protocol"] = "HTTP",
            ["protocolsettings"] = new JsonObject() { ["method"] = "POST" }
        };

        // act - create
        using var created = await fixture.Gateway.PostAsJsonAsync(Path, submitted);
        var subscription = JsonNode.Parse(await created.Content.ReadAsStringAsync())!;
        var id = subscription["id"]!.GetValue<string>();

        // assert - the subscription manager assigns the id, and returns the subscription as submitted
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.NotEqual("ignored", id);
        Assert.EndsWith($"/{Path}/{id}", created.Headers.Location!.OriginalString);
        Assert.Equal(submitted["filters"]!.ToJsonString(), subscription["filters"]!.ToJsonString());

        // act - events are published after the subscription, which starts at the end of the stream
        await Task.Delay(2000);
        await PublishAsync(Source(), "push-sdk", "com.github.push", "/cloudevents/sdk");
        await PublishAsync(Source(), "push-spec", "com.github.push", "/cloudevents/spec");
        await PublishAsync(Source(), "issue-sdk", "com.github.issue", "/cloudevents/sdk");
        await PublishAsync("https://e2e.cloud-streams.io/elsewhere", "push-elsewhere", "com.github.push", "/cloudevents/sdk");
        await PublishAsync(Source(), "pr-sdk", "com.github.pull_request.opened", "/cloudevents/sdk");

        // assert - only the matching events are delivered
        Assert.Equal(["push-sdk", "pr-sdk"], (await endpoint.WaitForAsync(2)).Select(d => d.Id));
        await Task.Delay(1000);
        Assert.Equal(2, endpoint.Deliveries.Count);

        // act - retrieve, list, update and delete
        var retrieved = JsonNode.Parse(await fixture.Gateway.GetStringAsync($"{Path}/{id}"))!;
        var listed = JsonNode.Parse(await fixture.Gateway.GetStringAsync(Path))!.AsArray();
        submitted["sink"] = $"{endpoint.Uri.OriginalString}?v=2";
        using var updated = await fixture.Gateway.PutAsJsonAsync($"{Path}/{id}", submitted.Deep(id));
        using var deleted = await fixture.Gateway.DeleteAsync($"{Path}/{id}");
        using var missing = await fixture.Gateway.GetAsync($"{Path}/{id}");

        // assert
        Assert.Equal(id, retrieved["id"]!.GetValue<string>());
        Assert.Equal(Source(), retrieved["source"]!.GetValue<string>());
        Assert.Contains(listed, s => s!["id"]!.GetValue<string>() == id);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.EndsWith("?v=2", JsonNode.Parse(await updated.Content.ReadAsStringAsync())!["sink"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Unsupported_Subscriptions_Should_Be_Refused_With_The_Reason()
    {
        // act
        using var response = await fixture.Gateway.PostAsJsonAsync(Path, new { sink = "https://subscriber.example.com", protocol = "HTTP", filters = new[] { new { sql = "source LIKE '%cloudevents%'" } } });

        // assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("'sql' filter dialect", JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("detail").GetString());
    }

}

static class JsonObjectExtensions
{

    /// <summary>
    /// Copies the specified subscription, with the specified id
    /// </summary>
    public static JsonObject Deep(this JsonObject subscription, string id)
    {
        var copy = subscription.DeepClone().AsObject();
        copy["id"] = id;
        return copy;
    }

}
