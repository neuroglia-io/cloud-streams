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

using CloudStreams.Core.Kubernetes;
using System.Text.Json.Nodes;

namespace CloudStreams.UnitTests.Cases.Core;

/// <summary>
/// Covers the CustomResourceDefinitions generated from the definitions of Cloud Streams, which the Helm chart installs (see #276)
/// </summary>
/// <remarks>
/// Set the <c>CLOUDSTREAMS_UPDATE_CRDS</c> environment variable to <c>true</c> to write the generated CRDs to the chart rather than to compare them
/// </remarks>
public class CustomResourceDefinitionTests
{

    static string ChartCrdsDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "CloudStreams.sln"))) directory = directory.Parent;
        return Path.Combine(directory?.FullName ?? throw new DirectoryNotFoundException("The root of the repository could not be found"), "deployments", "helm", "cloud-streams", "crds");
    }

    [Fact]
    public void The_Chart_Should_Ship_The_Generated_Definitions()
    {
        // arrange
        var directory = ChartCrdsDirectory();
        var generated = CustomResourceDefinitionGenerator.GenerateAll().ToList();
        if (string.Equals(Environment.GetEnvironmentVariable("CLOUDSTREAMS_UPDATE_CRDS"), "true", StringComparison.OrdinalIgnoreCase))
        {
            Directory.CreateDirectory(directory);
            foreach (var (fileName, yaml) in generated) File.WriteAllText(Path.Combine(directory, fileName), yaml);
        }

        // assert
        Assert.Equal(6, generated.Count);
        Assert.Equal(generated.Select(g => g.FileName).Order(), Directory.GetFiles(directory, "*.yaml").Select(Path.GetFileName).Order());
        foreach (var (fileName, yaml) in generated)
            Assert.True(yaml == File.ReadAllText(Path.Combine(directory, fileName)).ReplaceLineEndings("\n"), $"The CRD '{fileName}' of the chart is not the one generated from its definition: run the tests with CLOUDSTREAMS_UPDATE_CRDS=true to update it");
    }

    [Fact]
    public void A_Field_Of_Several_Types_Should_Preserve_Its_Value()
    {
        // arrange
        var schema = JsonNode.Parse("""{ "description": "An address or an object", "oneOf": [ { "type": "string" }, { "type": "object", "properties": { "uri": { "type": "string" } } } ] }""")!.AsObject();

        // act
        CustomResourceDefinitionGenerator.MakeStructural(schema, false);

        // assert
        Assert.Equal("""{"description":"An address or an object","x-kubernetes-preserve-unknown-fields":true}""", schema.ToJsonString());
    }

    [Fact]
    public void Junctors_Should_Only_Hold_Value_Validations()
    {
        // arrange
        var schema = JsonNode.Parse("""{ "type": "object", "properties": { "type": { "type": "string" }, "exponent": { "type": "number", "exclusiveMinimum": 0 } }, "anyOf": [ { "properties": { "type": { "const": "exponential", "description": "x" } }, "required": [ "type", "exponent" ] } ] }""")!.AsObject();

        // act
        CustomResourceDefinitionGenerator.MakeStructural(schema, false);

        // assert
        Assert.Equal(0, schema["properties"]!["exponent"]!["minimum"]!.GetValue<int>());
        Assert.True(schema["properties"]!["exponent"]!["exclusiveMinimum"]!.GetValue<bool>());
        var branch = schema["anyOf"]![0]!["properties"]!["type"]!.AsObject();
        Assert.Equal("""{"enum":["exponential"]}""", branch.ToJsonString());
    }

    [Fact]
    public void A_Nullable_Type_Should_Be_Written_As_Nullable()
    {
        var schema = JsonNode.Parse("""{ "type": [ "string", "null" ] }""")!.AsObject();
        CustomResourceDefinitionGenerator.MakeStructural(schema, false);
        Assert.Equal("""{"type":"string","nullable":true}""", schema.ToJsonString());
    }

}
