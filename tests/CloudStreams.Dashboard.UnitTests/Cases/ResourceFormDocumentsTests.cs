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
using CloudStreams.Dashboard.Services;
using Json.Patch;
using Xunit;
using JsonSerializer = Neuroglia.Serialization.Json.JsonSerializer;
using YamlSerializer = Neuroglia.Serialization.Yaml.YamlSerializer;

namespace CloudStreams.Dashboard.UnitTests.Cases;

/// <summary>
/// Covers how resource forms convert what they edit: the form and the YAML view edit the same resource, and the review shows and saves only what changed (see #260)
/// </summary>
public class ResourceFormDocumentsTests
{

    static Subscription Subscription() => new()
    {
        Metadata = new() { Name = "orders", Generation = 3, ResourceVersion = "42", Labels = new Dictionary<string, string>() { ["team"] = "orders" } },
        Spec = new() { Subscriber = new() { Endpoint = new Uri("https://orders.example.com/events") }, Stream = new() { Offset = -1 } },
        Status = new() { Phase = SubscriptionStatusPhase.Active }
    };

    [Fact]
    public void A_Copy_Should_Not_Share_Anything_With_The_Original()
    {
        // arrange
        var original = Subscription();

        // act
        var copy = ResourceFormDocuments.Clone(original, JsonSerializer.Default);
        copy.Spec.Subscriber.RateLimit = 5;
        copy.Metadata.Labels!["team"] = "billing";

        // assert
        Assert.Null(original.Spec.Subscriber.RateLimit);
        Assert.Equal("orders", original.Metadata.Labels!["team"]);
    }

    [Fact]
    public void The_Yaml_View_Should_Change_The_Spec_And_Labels_But_Keep_The_Rest()
    {
        // arrange
        var resource = Subscription();
        var yaml = ResourceFormDocuments.ToYaml(resource, JsonSerializer.Default, YamlSerializer.Default)
            .Replace("team: orders", "team: billing")
            .Replace("endpoint: https://orders.example.com/events", "endpoint: https://billing.example.com/events\n    rateLimit: 10");

        // act
        var applied = ResourceFormDocuments.Apply(resource, yaml, false, JsonSerializer.Default, YamlSerializer.Default);

        // assert
        Assert.Equal("billing", applied.Metadata.Labels!["team"]);
        Assert.Equal("https://billing.example.com/events", applied.Spec.Subscriber.Endpoint!.Uri.OriginalString);
        Assert.Equal(10, applied.Spec.Subscriber.RateLimit);
        Assert.Equal(3ul, applied.Metadata.Generation);
        Assert.Equal("42", applied.Metadata.ResourceVersion);
        Assert.Equal(SubscriptionStatusPhase.Active, applied.Status!.Phase);
    }

    [Fact]
    public void A_New_Resource_Should_Be_Renamed_From_The_Yaml_View_But_Not_An_Existing_One()
    {
        // arrange
        var yaml = ResourceFormDocuments.ToYaml(Subscription(), JsonSerializer.Default, YamlSerializer.Default).Replace("name: orders", "name: invoices");

        // act
        var renamed = ResourceFormDocuments.Apply(Subscription(), yaml, true, JsonSerializer.Default, YamlSerializer.Default);

        // assert
        Assert.Equal("invoices", renamed.GetName());
        Assert.Throws<FormatException>(() => ResourceFormDocuments.Apply(Subscription(), yaml, false, JsonSerializer.Default, YamlSerializer.Default));
    }

    [Theory]
    [InlineData("kind: Broker\nmetadata:\n  name: orders\nspec: {}\n")]
    [InlineData("metadata:\n  name: orders\n")]
    [InlineData("spec: [")]
    [InlineData("spec: {}\n---\nspec: {}\n")]
    public void Invalid_Yaml_Should_Be_Refused(string yaml)
    {
        Assert.Throws<FormatException>(() => ResourceFormDocuments.Apply(Subscription(), yaml, true, JsonSerializer.Default, YamlSerializer.Default));
    }

    [Fact]
    public void The_Patch_Should_Only_Hold_What_Changed()
    {
        // arrange
        var original = Subscription();
        var updated = ResourceFormDocuments.Clone(original, JsonSerializer.Default);
        updated.Spec.Subscriber.RateLimit = 10;
        updated.Status!.Phase = SubscriptionStatusPhase.Suspended;

        // act
        var patch = ResourceFormDocuments.GetPatch(original, updated, JsonSerializer.Default);
        var unchanged = ResourceFormDocuments.GetPatch(original, ResourceFormDocuments.Clone(original, JsonSerializer.Default), JsonSerializer.Default);

        // assert - the status is not the user's to change
        var operation = Assert.Single(JsonSerializer.Default.Deserialize<JsonPatch>(JsonSerializer.Default.SerializeToText(patch!.Document))!.Operations);
        Assert.Equal("/spec/subscriber/rateLimit", operation.Path.ToString());
        Assert.Null(unchanged);
    }

    [Fact]
    public void The_Diff_Should_Show_The_Changed_Lines()
    {
        // arrange
        var original = Subscription();
        var updated = ResourceFormDocuments.Clone(original, JsonSerializer.Default);
        updated.Spec.Subscriber.RateLimit = 10;

        // act
        var diff = ResourceFormDocuments.Diff(original, updated, JsonSerializer.Default, YamlSerializer.Default);

        // assert
        var added = Assert.Single(diff, l => l.Kind == TextDiffLineKind.Added);
        Assert.Contains("rateLimit: 10", added.Text);
        Assert.DoesNotContain(diff, l => l.Kind == TextDiffLineKind.Removed);
    }

    [Theory]
    [InlineData(30, "s", 30, "s")]
    [InlineData(120, "s", 2, "min")]
    [InlineData(48, "h", 2, "d")]
    [InlineData(1500, "ms", 1500, "ms")]
    [InlineData(0, "min", 0, "s")]
    public void Durations_Should_Be_Shown_In_The_Largest_Unit_They_Are_A_Whole_Number_Of(double amount, string unit, double expectedAmount, string expectedUnit)
    {
        // act
        var (shownAmount, shownUnit) = ResourceFormDocuments.ToAmount(ResourceFormDocuments.FromAmount(amount, unit));

        // assert
        Assert.Equal((expectedAmount, expectedUnit), (shownAmount, shownUnit));
    }

    [Theory]
    [InlineData("orders", true)]
    [InlineData("orders-audit-2", true)]
    [InlineData("a", true)]
    [InlineData("Orders", false)]
    [InlineData("-orders", false)]
    [InlineData("orders-", false)]
    [InlineData("orders_audit", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Resource_Names_Should_Be_Validated(string? name, bool valid)
    {
        Assert.Equal(valid, ResourceNames.IsValid(name));
        Assert.False(ResourceNames.IsValid(new string('a', ResourceNames.MaxLength + 1)));
    }

}
