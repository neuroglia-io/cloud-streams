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
using Neuroglia.Data;
using Neuroglia.Data.Infrastructure.ResourceOriented;
using Neuroglia.Eventing.CloudEvents;
using System.Net;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace CloudStreams.IntegrationTests.Cases;

/// <summary>
/// Publishes events to a gateway in every content mode of the CloudEvents HTTP binding, and verifies its deduplication, rate limits and request size limits
/// (#188, #208, #209, #210)
/// </summary>
/// <remarks>
/// Tests that configure the gateway's ingestion policy restore it once done: the tests of the collection share the gateway, but never run concurrently
/// </remarks>
[Collection(CloudStreamsCollection.Name)]
public sealed class IngestionTests(CloudStreamsFixture fixture, ITestOutputHelper output)
    : IAsyncLifetime
{

    const string GatewayName = "gateway-e2e";

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

    static string Structured(Uri source, string id) => $"{{\"specversion\":\"1.0\",\"id\":\"{id}\",\"source\":\"{source}\",\"type\":\"io.cloud-streams.e2e.v1\",\"data\":{{\"id\":\"{id}\"}}}}";

    Task<HttpResponseMessage> PublishStructuredAsync(Uri source, string id) => fixture.Gateway.PostAsync("api/events/pub", new StringContent(Structured(source, id), Encoding.UTF8, CloudEventContentType.Json));

    async Task<HttpResponseMessage> PublishBinaryAsync(Uri source, string id, byte[] data, string contentType)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/events/pub") { Content = new ByteArrayContent(data) };
        request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        request.Headers.Add("ce-specversion", "1.0");
        request.Headers.Add("ce-id", id);
        request.Headers.Add("ce-source", source.OriginalString);
        request.Headers.Add("ce-type", "io.cloud-streams.e2e.v1");
        request.Headers.Add("ce-tenant", "acme");
        return await fixture.Gateway.SendAsync(request);
    }

    async Task<Subscription> SubscribeAsync(string name, Uri source, TestSubscriber.Endpoint endpoint) => await fixture.Api.Resources.Subscriptions.CreateAsync(new Subscription()
    {
        Metadata = new() { Name = name },
        Spec = new() { Partition = new(CloudEventPartitionType.BySource, source.OriginalString), Subscriber = new() { Endpoint = endpoint.Uri }, Stream = new() { Offset = 0 } }
    });

    /// <summary>
    /// Sets the gateway's ingestion policy, and waits until it applies, which is once the gateway's watch has reported it
    /// </summary>
    /// <param name="ingestion">The ingestion policy to set, or null to remove it</param>
    /// <param name="inEffect">A probe that determines whether or not the policy under test is in effect. Each attempt must use new events</param>
    async Task ConfigureIngestionAsync(CloudEventIngestionPolicy? ingestion, Func<Task<bool>> inEffect)
    {
        var gateway = await fixture.Api.Resources.Gateways.GetAsync(GatewayName);
        gateway.Spec.Ingestion = ingestion;
        await fixture.Api.Resources.Gateways.UpdateAsync(gateway);
        // the next test must not be affected by a policy that has not been removed yet, either
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (await inEffect() != (ingestion != null))
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException($"The gateway's ingestion policy was not {(ingestion == null ? "removed" : "applied")} in time");
            await Task.Delay(250);
        }
    }

    [Fact]
    public async Task Binary_And_Batched_Events_Should_Be_Delivered_Like_Structured_Ones()
    {
        // arrange
        var endpoint = fixture.Subscriber.Register(Name());
        await SubscribeAsync(Name(), Source(), endpoint);

        // act - binary events, whose data is JSON and bytes
        using (var json = await PublishBinaryAsync(Source(), "binary-json", Encoding.UTF8.GetBytes("{\"total\":12}"), "application/json"))
            Assert.Equal(HttpStatusCode.NoContent, json.StatusCode);
        using (var bytes = await PublishBinaryAsync(Source(), "binary-bytes", [1, 2, 3], "application/octet-stream"))
            Assert.Equal(HttpStatusCode.NoContent, bytes.StatusCode);

        // act - a batch, whose second element is invalid
        var batch = $"[{Structured(Source(), "batch-0")}, {{\"id\":\"no-source\"}}, {Structured(Source(), "batch-2")}]";
        using var response = await fixture.Gateway.PostAsync("api/events/pub", new StringContent(batch, Encoding.UTF8, "application/cloudevents-batch+json"));

        // assert - each element of the batch has its own result
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var results = JsonSerializer.Deserialize<JsonElement[]>(await response.Content.ReadAsStringAsync())!;
        Assert.Equal([201, 400, 201], results.Select(r => r.GetProperty("status").GetInt32()));
        Assert.Equal("stored", results[0].GetProperty("outcome").GetString());
        Assert.Equal("batch-2", results[2].GetProperty("id").GetString());

        // assert - every stored event is delivered, in order, as it was published
        var deliveries = await endpoint.WaitForAsync(4);
        Assert.Equal(["binary-json", "binary-bytes", "batch-0", "batch-2"], deliveries.Select(d => d.Id));
        Assert.Equal(12, deliveries[0].Event.GetProperty("data").GetProperty("total").GetInt32());
        Assert.Equal("acme", deliveries[0].Event.GetProperty("tenant").GetString());
        Assert.Equal("AQID", deliveries[1].Event.GetProperty("data_base64").GetString());
        Assert.False(deliveries[1].Event.TryGetProperty("data", out var data) && data.ValueKind != JsonValueKind.Null);
    }

    [Fact]
    public async Task A_Request_In_An_Unsupported_Content_Mode_Should_Be_Refused()
    {
        using var response = await fixture.Gateway.PostAsync("api/events/pub", new StringContent("{}", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    [Fact]
    public async Task Duplicates_Should_Be_Answered_As_Stored_But_Delivered_Once()
    {
        // arrange
        var endpoint = fixture.Subscriber.Register(Name());
        await SubscribeAsync(Name(), Source(), endpoint);
        // the policy is in effect when publishing the same event twice is answered as a duplicate
        var probe = 0;
        async Task<bool> InEffect()
        {
            var id = $"probe-{probe++}";
            using var first = await PublishStructuredAsync(new("https://e2e.cloud-streams.io/probe"), id);
            using var second = await PublishStructuredAsync(new("https://e2e.cloud-streams.io/probe"), id);
            return second.Headers.TryGetValues(CloudStreamsHeaders.Outcome, out var outcome) && outcome.Single() == CloudStreamsHeaders.Outcomes.Duplicate;
        }
        try
        {
            await ConfigureIngestionAsync(new() { Deduplication = new() { Window = Duration.FromMinutes(5) } }, InEffect);

            // act
            var outcomes = new List<string>();
            foreach (var id in new[] { "order-1", "order-1", "order-2", "order-1" })
            {
                using var response = await PublishStructuredAsync(Source(), id);
                Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
                outcomes.Add(response.Headers.GetValues(CloudStreamsHeaders.Outcome).Single());
            }

            // assert
            Assert.Equal(["stored", "duplicate", "stored", "duplicate"], outcomes);
            Assert.Equal(["order-1", "order-2"], (await endpoint.WaitForAsync(2)).Select(d => d.Id));
            await Task.Delay(1000);
            Assert.Equal(2, endpoint.Deliveries.Count);
        }
        finally
        {
            await ConfigureIngestionAsync(null, InEffect);
        }
    }

    [Fact]
    public async Task Events_Beyond_A_Rate_Limit_Should_Be_Refused_With_The_Delay_After_Which_To_Retry()
    {
        // the policy is in effect when a new source cannot publish a 4th event
        var probe = 0;
        async Task<bool> InEffect()
        {
            var source = new Uri($"https://e2e.cloud-streams.io/rate-probe-{probe++}");
            var refused = false;
            for (var i = 0; i < 4; i++)
            {
                using var response = await PublishStructuredAsync(source, $"probe-{i}");
                refused = response.StatusCode == HttpStatusCode.TooManyRequests;
            }
            return refused;
        }
        try
        {
            // arrange - every source may publish 3 events a day
            await ConfigureIngestionAsync(new() { RateLimits = [new() { Partition = CloudEventRateLimitPartitioning.Source, Limit = 3, Period = Duration.FromDays(1) }] }, InEffect);

            // act
            var statuses = new List<HttpStatusCode>();
            TimeSpan? retryAfter = null;
            for (var i = 0; i < 4; i++)
            {
                using var response = await PublishStructuredAsync(Source(), $"event-{i}");
                statuses.Add(response.StatusCode);
                retryAfter ??= response.Headers.RetryAfter?.Delta;
            }
            using var batch = await fixture.Gateway.PostAsync("api/events/pub", new StringContent($"[{Structured(Source(), "batched")}]", Encoding.UTF8, "application/cloudevents-batch+json"));

            // assert
            Assert.Equal([HttpStatusCode.NoContent, HttpStatusCode.NoContent, HttpStatusCode.NoContent, HttpStatusCode.TooManyRequests], statuses);
            Assert.NotNull(retryAfter);
            Assert.InRange(retryAfter!.Value, TimeSpan.FromSeconds(1), TimeSpan.FromDays(1));
            var results = JsonSerializer.Deserialize<JsonElement[]>(await batch.Content.ReadAsStringAsync())!;
            Assert.Equal(429, Assert.Single(results).GetProperty("status").GetInt32());
            Assert.NotNull(batch.Headers.RetryAfter);
        }
        finally
        {
            await ConfigureIngestionAsync(null, InEffect);
        }
    }

    [Fact]
    public async Task Requests_Larger_Than_The_Limit_Should_Be_Refused()
    {
        // the policy is in effect when an event of 2 KiB is refused
        var probe = 0;
        async Task<bool> InEffect()
        {
            using var response = await PublishBinaryAsync(new("https://e2e.cloud-streams.io/size-probe"), $"probe-{probe++}", new byte[2048], "application/octet-stream");
            return response.StatusCode == HttpStatusCode.RequestEntityTooLarge;
        }
        try
        {
            // arrange
            await ConfigureIngestionAsync(new() { MaxRequestBodySize = 1024, MaxBatchSize = 2 }, InEffect);

            // act
            using var small = await PublishStructuredAsync(Source(), "small");
            using var large = await PublishBinaryAsync(Source(), "large", new byte[4096], "application/octet-stream");
            var batch = $"[{Structured(Source(), "b-0")}, {Structured(Source(), "b-1")}, {Structured(Source(), "b-2")}]";
            using var tooManyEvents = await fixture.Gateway.PostAsync("api/events/pub", new StringContent(batch, Encoding.UTF8, "application/cloudevents-batch+json"));

            // assert
            Assert.Equal(HttpStatusCode.NoContent, small.StatusCode);
            Assert.Equal(HttpStatusCode.RequestEntityTooLarge, large.StatusCode);
            Assert.Equal(HttpStatusCode.RequestEntityTooLarge, tooManyEvents.StatusCode);
        }
        finally
        {
            await ConfigureIngestionAsync(null, InEffect);
        }
    }

}
