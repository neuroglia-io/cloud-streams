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

using CloudStreams.Core.Api.Client.Services;
using CloudStreams.Core.Resources;
using Json.Patch;
using Json.Pointer;
using Neuroglia;
using Neuroglia.Data;
using Neuroglia.Data.Infrastructure.ResourceOriented;
using System.Net;
using System.Text.Json.Nodes;
using Xunit;

namespace CloudStreams.IntegrationTests.Cases;

/// <summary>
/// Verifies, end to end, the dry runs the dashboard's forms validate resources with before saving them (see #260)
/// </summary>
[Collection(CloudStreamsCollection.Name)]
public sealed class DryRunTests(CloudStreamsFixture fixture)
{

    static Subscription Subscription(string name, string uri = "https://dry-run.example.com/events") => new()
    {
        Metadata = new() { Name = name },
        Spec = new() { Subscriber = new() { Uri = new(uri) } }
    };

    [Fact]
    public async Task A_Dry_Run_Should_Validate_Without_Saving()
    {
        // act
        var validated = await fixture.Api.Resources.Subscriptions.CreateAsync(Subscription("dry-run-create"), true);
        var missing = await Assert.ThrowsAsync<ProblemDetailsException>(() => fixture.Api.Resources.Subscriptions.GetAsync("dry-run-create"));

        // assert
        Assert.Equal("dry-run-create", validated.GetName());
        Assert.Equal((int)HttpStatusCode.NotFound, missing.Problem.Status);
    }

    [Fact]
    public async Task A_Refused_Dry_Run_Should_Report_The_Errors_Of_Each_Field()
    {
        // arrange
        var subscription = Subscription("dry-run-invalid", "ftp://dry-run.example.com");
        subscription.Spec.Subscriber.RateLimit = -1;

        // act
        var refused = await Assert.ThrowsAsync<ProblemDetailsException>(() => fixture.Api.Resources.Subscriptions.CreateAsync(subscription, true));

        // assert
        Assert.Equal((int)HttpStatusCode.BadRequest, refused.Problem.Status);
        Assert.Contains("spec.subscriber.uri", refused.Problem.Errors!.Keys);
        Assert.Contains("spec.subscriber.rateLimit", refused.Problem.Errors.Keys);
    }

    [Fact]
    public async Task A_Dry_Run_Patch_Should_Validate_Without_Saving()
    {
        // arrange
        await fixture.Api.Resources.Subscriptions.CreateAsync(Subscription("dry-run-patch"));
        var valid = new Patch(PatchType.JsonPatch, new JsonPatch(PatchOperation.Add(JsonPointer.Parse("/spec/subscriber/rateLimit"), JsonValue.Create(5))));
        var invalid = new Patch(PatchType.JsonPatch, new JsonPatch(PatchOperation.Add(JsonPointer.Parse("/spec/subscriber/rateLimit"), JsonValue.Create(-5))));

        // act
        await fixture.Api.Resources.Subscriptions.PatchAsync(valid, "dry-run-patch", null, true);
        var refused = await Assert.ThrowsAsync<ProblemDetailsException>(() => fixture.Api.Resources.Subscriptions.PatchAsync(invalid, "dry-run-patch", null, true));
        var subscription = await fixture.Api.Resources.Subscriptions.GetAsync("dry-run-patch");

        // assert
        Assert.Null(subscription.Spec.Subscriber.RateLimit);
        Assert.Equal(["spec.subscriber.rateLimit"], refused.Problem.Errors!.Keys);
    }

}
