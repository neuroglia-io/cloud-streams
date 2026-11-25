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
using Neuroglia;
using Neuroglia.Data.Infrastructure.ResourceOriented;
using Neuroglia.Eventing.CloudEvents;
using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Xunit;
using Xunit.Abstractions;

namespace CloudStreams.IntegrationTests.Cases;

/// <summary>
/// Registers, versions and binds JSON schemas, end to end (#213)
/// </summary>
[Collection(CloudStreamsCollection.Name)]
public sealed class SchemaRegistryTests(CloudStreamsFixture fixture, ITestOutputHelper output)
    : IAsyncLifetime
{

    const string GatewayName = "gateway-e2e";

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

    static readonly JsonNode Order = JsonNode.Parse("""{ "type": "object", "properties": { "id": { "type": "string" }, "total": { "type": "number", "minimum": 0 } }, "required": [ "id", "total" ] }""")!;

    // backward compatible: 'total' is no longer required
    static readonly JsonNode OptionalTotal = JsonNode.Parse("""{ "type": "object", "properties": { "id": { "type": "string" }, "total": { "type": "number", "minimum": 0 } }, "required": [ "id" ] }""")!;

    // backward incompatible: 'currency' is now required
    static readonly JsonNode RequiringCurrency = JsonNode.Parse("""{ "type": "object", "properties": { "id": { "type": "string" }, "total": { "type": "number", "minimum": 0 }, "currency": { "type": "string" } }, "required": [ "id", "total", "currency" ] }""")!;

    Task<HttpResponseMessage> PublishAsync(string source, string id, object data) => fixture.Gateway.PostAsync("api/events/pub", new StringContent(
        JsonSerializer.Serialize(new { specversion = "1.0", id, source, type = "io.cloud-streams.e2e.order.v1", data }), Encoding.UTF8, CloudEventContentType.Json));

    [Fact]
    public async Task Schemas_Should_Be_Versioned_Compatibly_And_Bound_To_Events()
    {
        // arrange
        var schema = await fixture.Api.Resources.Schemas.CreateAsync(new Schema(new(Name()), new() { Versions = [new() { Version = 1, Schema = Order }] }));

        // act - an incompatible version is refused, and the compatibility check tells why without changing anything
        schema.Spec.Versions.Add(new() { Version = 2, Schema = RequiringCurrency });
        var refused = await Assert.ThrowsAsync<ProblemDetailsException>(() => fixture.Api.Resources.Schemas.UpdateAsync(schema));
        using var incompatible = await fixture.Gateway.PostAsJsonAsync($"api/resources/v1/schemas/{Name()}/compatibility", RequiringCurrency);
        using var compatible = await fixture.Gateway.PostAsJsonAsync($"api/resources/v1/schemas/{Name()}/compatibility", OptionalTotal);

        // assert
        Assert.Equal((int)HttpStatusCode.BadRequest, refused.Problem.Status);
        Assert.Contains("not backward compatible", refused.Problem.Detail);
        var incompatibility = JsonNode.Parse(await incompatible.Content.ReadAsStringAsync())!;
        Assert.False(incompatibility["compatible"]!.GetValue<bool>());
        Assert.Contains(incompatibility["errors"]!.AsArray(), e => e!.GetValue<string>().Contains("'currency' is required"));
        Assert.True(JsonNode.Parse(await compatible.Content.ReadAsStringAsync())!["compatible"]!.GetValue<bool>());

        // act - a compatible version is added, and served as the latest one
        schema = await fixture.Api.Resources.Schemas.GetAsync(Name());
        schema.Spec.Versions.Add(new() { Version = 2, Schema = OptionalTotal });
        await fixture.Api.Resources.Schemas.UpdateAsync(schema);
        var latest = JsonNode.Parse(await fixture.Gateway.GetStringAsync($"api/resources/v1/schemas/{Name()}/versions/latest"))!;
        var first = JsonNode.Parse(await fixture.Gateway.GetStringAsync($"api/resources/v1/schemas/{Name()}/versions/1"))!;

        // assert
        Assert.True(JsonNode.DeepEquals(OptionalTotal, latest));
        Assert.True(JsonNode.DeepEquals(Order, first));

        // act - the gateway binds the events of the test's source to version 1
        try
        {
            await this.BindAsync(new() { Name = Name(), Version = 1 });
            using var valid = await PublishAsync(Source(), "valid", new { id = "o-1", total = 12 });
            using var invalid = await PublishAsync(Source(), "invalid", new { id = "o-2" });
            using var unbound = await PublishAsync("https://e2e.cloud-streams.io/elsewhere", "unbound", new { id = "o-3" });

            // assert
            Assert.Equal(HttpStatusCode.NoContent, valid.StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
            Assert.Contains($"version 1 of the schema '{Name()}'", JsonDocument.Parse(await invalid.Content.ReadAsStringAsync()).RootElement.GetProperty("detail").GetString());
            Assert.Equal(HttpStatusCode.NoContent, unbound.StatusCode);
        }
        finally
        {
            await this.BindAsync(null);
        }
    }

    /// <summary>
    /// Binds the events of the test's source to the specified schema, or removes the binding, and waits until the gateway applies the change
    /// </summary>
    async Task BindAsync(SchemaReference? reference, [CallerMemberName] string test = "")
    {
        var gateway = await fixture.Api.Resources.Gateways.GetAsync(GatewayName);
        gateway.Spec.Events = reference == null ? null : [new() { Source = $"^{Regex.Escape(Source(test))}$", Type = "*", Schema = reference }];
        await fixture.Api.Resources.Gateways.UpdateAsync(gateway);
        // the binding applies once data that version 1 refuses is refused
        var deadline = DateTime.UtcNow.AddSeconds(30);
        var probe = 0;
        while (true)
        {
            using var response = await PublishAsync(Source(test), $"probe-{probe++}", new { id = "probe" });
            if ((response.StatusCode == HttpStatusCode.BadRequest) == (reference != null)) return;
            if (DateTime.UtcNow > deadline) throw new TimeoutException("The gateway did not apply the binding in time");
            await Task.Delay(250);
        }
    }

}
