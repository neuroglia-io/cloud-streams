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

using CloudStreams.Core.Application.Commands.Resources.Generic;
using CloudStreams.Core.Application.Validation;
using Json.Patch;
using Json.Pointer;
using Neuroglia.Data.PatchModel.Services;
using System.Text.Json.Nodes;

namespace CloudStreams.UnitTests.Cases.Core;

/// <summary>
/// Covers dry runs, which forms rely on to validate a resource before saving it: nothing is saved, and refusals carry the errors of each field (see #260)
/// </summary>
public class ResourceDryRunTests
{

    static Subscription Subscription(string uri = "https://orders.example.com/events") => new()
    {
        Metadata = new() { Name = "orders" },
        Spec = new() { Subscriber = new() { Endpoint = new Uri(uri) } }
    };

    static ResourceDryRun DryRun() => new([new CloudStreamsResourceValidator(Neuroglia.Serialization.Json.JsonSerializer.Default, [new JsonPatchHandler()])]);

    static IResourceRepository Repository(Subscription? existing)
    {
        var repository = Substitute.For<IResourceRepository>();
        repository.GetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult<IResource?>(existing));
        return repository;
    }

    [Fact]
    public async Task A_Dry_Run_Creation_Should_Validate_Without_Saving()
    {
        // arrange
        var repository = Repository(null);

        // act
        var result = await new CreateResourceCommandHandler<Subscription>(repository, DryRun()).HandleAsync(new(Subscription(), true), CancellationToken.None);

        // assert - the repository used to persist resources created with a dry run
        Assert.Equal(200, result.Status);
        await repository.DidNotReceiveWithAnyArgs().AddAsync(default!, default, default, default, default, default);
    }

    [Fact]
    public async Task A_Refused_Dry_Run_Should_Report_The_Errors_Of_Each_Field()
    {
        // arrange
        var subscription = Subscription("ftp://orders.example.com");
        subscription.Spec.Subscriber.RateLimit = -1;

        // act
        var result = await new CreateResourceCommandHandler<Subscription>(Repository(null), DryRun()).HandleAsync(new(subscription, true), CancellationToken.None);

        // assert
        Assert.Equal(400, result.Status);
        var errors = Assert.Single(result.Errors!).Errors!.ToDictionary(e => e.Key, e => e.Value);
        Assert.Contains("spec.subscriber.endpoint", errors.Keys);
        Assert.Contains("spec.subscriber.rateLimit", errors.Keys);
    }

    [Fact]
    public async Task A_Dry_Run_Creation_Of_An_Existing_Resource_Should_Conflict()
    {
        // act
        var result = await new CreateResourceCommandHandler<Subscription>(Repository(Subscription()), DryRun()).HandleAsync(new(Subscription(), true), CancellationToken.None);

        // assert
        Assert.Equal(409, result.Status);
    }

    [Fact]
    public async Task A_Refused_Dry_Run_Patch_Should_Report_The_Errors_Of_Each_Field_Without_Patching()
    {
        // arrange
        var repository = Repository(Subscription());
        var patch = new Patch(PatchType.JsonPatch, new JsonPatch(PatchOperation.Add(JsonPointer.Parse("/spec/subscriber/rateLimit"), JsonValue.Create(-3))));

        // act
        var result = await new PatchResourceCommandHandler<Subscription>(repository, DryRun()).HandleAsync(new("orders", null, patch, true), CancellationToken.None);

        // assert
        Assert.Equal(400, result.Status);
        Assert.Equal(["spec.subscriber.rateLimit"], Assert.Single(result.Errors!).Errors!.Select(e => e.Key));
        await repository.DidNotReceiveWithAnyArgs().PatchAsync(default!, default!, default!, default!, default!, default, default, default, default);
    }

    [Fact]
    public async Task A_Dry_Run_Patch_Of_A_Missing_Resource_Should_Not_Be_Found()
    {
        // arrange
        var patch = new Patch(PatchType.JsonPatch, new JsonPatch(PatchOperation.Add(JsonPointer.Parse("/spec/suspended"), JsonValue.Create(true))));

        // act
        var result = await new PatchResourceCommandHandler<Subscription>(Repository(null), DryRun()).HandleAsync(new("orders", null, patch, true), CancellationToken.None);

        // assert
        Assert.Equal(404, result.Status);
    }

}
