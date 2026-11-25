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
using Neuroglia.Data.Infrastructure.ResourceOriented;
using Neuroglia.Eventing.CloudEvents;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using Xunit;
using Xunit.Abstractions;

namespace CloudStreams.IntegrationTests.Cases;

/// <summary>
/// Covers the APIs the gateway and schema forms use to check what they edit before saving it (#262, #264)
/// </summary>
[Collection(CloudStreamsCollection.Name)]
public sealed class ResourceFormApiTests(CloudStreamsFixture fixture, ITestOutputHelper output)
    : IAsyncLifetime
{

    const string GatewayName = "gateway-e2e";

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

    static CloudEvent Event(string source) => new()
    {
        Id = Guid.NewGuid().ToString(),
        SpecVersion = CloudEventSpecVersion.V1.Version,
        Source = new(source),
        Type = "io.cloud-streams.e2e.form.v1",
        DataContentType = "application/json",
        Data = new Dictionary<string, object>() { ["id"] = "1234" }
    };

    [Fact]
    public async Task An_Event_Should_Be_Tested_Against_An_Unsaved_Gateway_Spec_Without_Being_Stored()
    {
        // arrange
        var spec = new GatewaySpec()
        {
            Authorization = new()
            {
                DecisionStrategy = RuleBasedDecisionStrategy.Unanimous,
                Rules = [new() { Type = CloudEventAuthorizationRuleType.Attribute, Effect = AuthorizationPolicyEffect.Authorize, AttributeName = "source", AttributeValue = "^https://allowed\\.example\\.com$" }]
            }
        };
        var allowed = Event("https://allowed.example.com");
        var denied = Event("https://denied.example.com");

        // act
        var accepted = await fixture.Api.Resources.Gateways.TestAdmissionAsync(GatewayName, new() { Spec = spec, Event = allowed });
        var rejected = await fixture.Api.Resources.Gateways.TestAdmissionAsync(GatewayName, new() { Spec = spec, Event = denied });
        var running = await fixture.Api.Resources.Gateways.GetAsync(GatewayName);

        // assert
        Assert.True(accepted.Outcome == GatewayAdmissionTestResult.Accepted, $"{accepted.Outcome} {accepted.Status}: {accepted.Reason} {string.Join("; ", accepted.Errors?.Select(e => $"{e.Key}={string.Join(",", e.Value)}") ?? [])}");
        Assert.Equal(allowed.Id, accepted.Attributes!["id"]?.ToString());
        Assert.Equal(GatewayAdmissionTestResult.Rejected, rejected.Outcome);
        Assert.NotNull(rejected.Reason);
        Assert.Null(running.Spec.Authorization);
        Assert.Equal(0, await this.CountEventsAsync(allowed.Source.OriginalString));
    }

    /// <summary>
    /// Counts the stored events of the specified source, of which there are none if its partition does not exist
    /// </summary>
    async Task<int> CountEventsAsync(string source)
    {
        try { return await (await fixture.Api.CloudEvents.Stream.ReadStreamAsync(new(new PartitionReference(CloudEventPartitionType.BySource, source)))).CountAsync(); }
        catch (Neuroglia.ProblemDetailsException ex) when (ex.Problem.Status == 404) { return 0; }
    }

    [Fact]
    public async Task A_Candidate_Version_Should_Be_Checked_Without_Being_Added()
    {
        // arrange
        var first = JsonNode.Parse("""{ "type": "object", "properties": { "id": { "type": "string" } }, "required": [ "id" ] }""")!;
        var requiringTotal = JsonNode.Parse("""{ "type": "object", "properties": { "id": { "type": "string" }, "total": { "type": "number" } }, "required": [ "id", "total" ] }""")!;
        var optionalId = JsonNode.Parse("""{ "type": "object", "properties": { "id": { "type": "string" } } }""")!;
        await fixture.Api.Resources.Schemas.CreateAsync(new Schema(new(Name()), new() { Versions = [new() { Version = 1, Schema = first }] }));

        // act
        var incompatible = await fixture.Api.Resources.Schemas.CheckCompatibilityAsync(Name(), requiringTotal);
        var compatible = await fixture.Api.Resources.Schemas.CheckCompatibilityAsync(Name(), optionalId);
        var schema = await fixture.Api.Resources.Schemas.GetAsync(Name());

        // assert
        Assert.False(incompatible.Compatible);
        Assert.NotEmpty(incompatible.Errors);
        Assert.True(compatible.Compatible);
        Assert.Single(schema.Spec.Versions);
    }

}
