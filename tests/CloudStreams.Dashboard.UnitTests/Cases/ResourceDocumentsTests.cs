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
using CloudStreams.Dashboard.Services;
using Xunit;
using JsonSerializer = Neuroglia.Serialization.Json.JsonSerializer;
using YamlSerializer = Neuroglia.Serialization.Yaml.YamlSerializer;

namespace CloudStreams.Dashboard.UnitTests.Cases;

/// <summary>
/// Covers the documents resources are exported as, imported from and compared with before saving (see #227)
/// </summary>
public class ResourceDocumentsTests
{

    static Subscription Subscription(string name) => new()
    {
        Metadata = new()
        {
            Name = name,
            Labels = new Dictionary<string, string>() { ["tier"] = "edge" },
            Generation = 3,
            ResourceVersion = "42",
            CreationTimestamp = DateTimeOffset.UtcNow
        },
        Spec = new() { Subscriber = new() { Uri = new($"https://{name}.example.com/events"), RateLimit = 5 } },
        Status = new() { Phase = SubscriptionStatusPhase.Active }
    };

    [Fact]
    public void Exported_Resources_Should_Only_Hold_What_Users_Own_And_Be_Imported_Back()
    {
        // act
        var yaml = ResourceDocuments.ExportYaml([Subscription("b"), Subscription("a")], JsonSerializer.Default, YamlSerializer.Default);
        var documents = ResourceDocuments.ParseDocuments(yaml, YamlSerializer.Default);

        // assert
        Assert.Equal(["a", "b"], documents.Select(d => d["metadata"]!["name"]!.GetValue<string>()));
        Assert.All(documents, d =>
        {
            Assert.Equal("Subscription", d["kind"]!.GetValue<string>());
            Assert.Equal("edge", d["metadata"]!["labels"]!["tier"]!.GetValue<string>());
            Assert.Null(d["status"]);
            Assert.Null(d["metadata"]!["resourceVersion"]);
            Assert.Null(d["metadata"]!["generation"]);
            Assert.Equal(5, d["spec"]!["subscriber"]!["rateLimit"]!.GetValue<int>());
        });
    }

    [Theory]
    [InlineData("""{"kind":"Subscription","metadata":{"name":"a"}}""", 1)]
    [InlineData("""[{"metadata":{"name":"a"}},{"metadata":{"name":"b"}}]""", 2)]
    [InlineData("---\nmetadata:\n  name: a\n---\n# a comment\n---\nmetadata:\n  name: b\n...\n", 2)]
    public void Documents_Should_Be_Parsed_From_Json_Or_Yaml_Streams(string text, int count) => Assert.Equal(count, ResourceDocuments.ParseDocuments(text, YamlSerializer.Default).Count);

    [Theory]
    [InlineData("[1, 2]")]
    [InlineData("{ not json")]
    [InlineData("- a\n- b\n")]
    public void Invalid_Documents_Should_Be_Refused(string text) => Assert.Throws<FormatException>(() => ResourceDocuments.ParseDocuments(text, YamlSerializer.Default));

    [Fact]
    public void Diffs_Should_Keep_Common_Lines_And_Mark_Removed_And_Added_Ones()
    {
        // act
        var diff = TextDiff.Compute("a\nb\nc\nd", "a\nc\nx\nd\ne");

        // assert
        Assert.Equal(
            ["  a", "- b", "  c", "+ x", "  d", "+ e"],
            diff.Select(l => (l.Kind switch { TextDiffLineKind.Added => "+ ", TextDiffLineKind.Removed => "- ", _ => "  " }) + l.Text));
        Assert.All(TextDiff.Compute("same\n", "same"), l => Assert.Equal(TextDiffLineKind.Unchanged, l.Kind));
    }

}
