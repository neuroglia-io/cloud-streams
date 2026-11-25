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

using CloudStreams.Dashboard.Services;
using Json.Schema;
using System.Text.Json.Nodes;
using Xunit;

namespace CloudStreams.Dashboard.UnitTests.Cases;

/// <summary>
/// Covers the JSON schemas the schema form generates from the data of an event (see #264)
/// </summary>
public class JsonSchemaInferenceTests
{

    static readonly JsonNode Sample = JsonNode.Parse("""
        { "id": "1234", "total": 42.5, "quantity": 3, "paid": true, "note": null, "lines": [ { "sku": "a", "count": 1 } ], "tags": [], "customer": { "name": "Ada" } }
        """)!;

    [Fact]
    public void The_Sample_Should_Be_Valid_Against_The_Inferred_Schema()
    {
        // arrange
        var schema = JsonSchema.FromText(JsonSchemaInference.Infer(Sample).ToJsonString());

        // act
        var result = schema.Evaluate(Sample);

        // assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void The_Inferred_Schema_Should_Describe_The_Types_Of_The_Sample()
    {
        // act
        var schema = JsonSchemaInference.Infer(Sample);

        // assert
        Assert.Equal(JsonSchemaInference.Dialect, schema["$schema"]!.GetValue<string>());
        var properties = schema["properties"]!;
        Assert.Equal("string", properties["id"]!["type"]!.GetValue<string>());
        Assert.Equal("number", properties["total"]!["type"]!.GetValue<string>());
        Assert.Equal("integer", properties["quantity"]!["type"]!.GetValue<string>());
        Assert.Equal("boolean", properties["paid"]!["type"]!.GetValue<string>());
        Assert.Equal("null", properties["note"]!["type"]!.GetValue<string>());
        Assert.Equal("string", properties["lines"]!["items"]!["properties"]!["sku"]!["type"]!.GetValue<string>());
        Assert.Null(properties["tags"]!["items"]);
        Assert.Equal("string", properties["customer"]!["properties"]!["name"]!["type"]!.GetValue<string>());
    }

    [Fact]
    public void Properties_Should_Be_Required_Unless_They_Are_Null()
    {
        // act
        var required = JsonSchemaInference.Infer(Sample)["required"]!.AsArray().Select(n => n!.GetValue<string>()).ToList();

        // assert
        Assert.Contains("id", required);
        Assert.Contains("customer", required);
        Assert.DoesNotContain("note", required);
    }

    [Fact]
    public void A_Document_With_A_Missing_Required_Property_Should_Be_Invalid()
    {
        // arrange
        var schema = JsonSchema.FromText(JsonSchemaInference.Infer(Sample).ToJsonString());
        var document = Sample.DeepClone().AsObject();
        document.Remove("id");

        // act
        var result = schema.Evaluate(document);

        // assert
        Assert.False(result.IsValid);
    }

}
