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

using CloudStreams.Core.Application.Validation;
using System.Text.Json.Nodes;

namespace CloudStreams.UnitTests.Cases.Core;

/// <summary>
/// Covers the compatibility of schema versions, and the rules the versions of registered schemas follow (see #213)
/// </summary>
public class SchemaRegistryTests
{

    const string Order = """
        { "type": "object", "properties": { "id": { "type": "string" }, "total": { "type": "number", "minimum": 0 } }, "required": [ "id" ] }
        """;

    static JsonNode Json(string json) => JsonNode.Parse(json)!;

    static IReadOnlyList<string> Check(string previous, string next, string compatibility = SchemaCompatibility.Backward) => SchemaCompatibilityChecker.Check(Json(previous), Json(next), compatibility);

    public static TheoryData<string, string> BackwardCompatibleChanges => new()
    {
        { "making a property optional", """{ "type": "object", "properties": { "id": { "type": "string" }, "total": { "type": "number", "minimum": 0 } } }""" },
        { "widening a type", """{ "type": "object", "properties": { "id": { "type": [ "string", "integer" ] }, "total": { "type": "number", "minimum": 0 } }, "required": [ "id" ] }""" },
        { "relaxing a bound", """{ "type": "object", "properties": { "id": { "type": "string" }, "total": { "type": "number", "minimum": -10 } }, "required": [ "id" ] }""" },
        { "changing annotations only", """{ "title": "Order", "type": "object", "properties": { "id": { "type": "string", "description": "The id" }, "total": { "type": "number", "minimum": 0 } }, "required": [ "id" ] }""" },
        { "removing every constraint", "true" }
    };

    [Theory]
    [MemberData(nameof(BackwardCompatibleChanges))]
    public void Backward_Compatible_Changes_Should_Be_Accepted(string change, string next)
    {
        Assert.True(Check(Order, next).Count == 0, $"Expected {change} to be backward compatible, got: {string.Join("; ", Check(Order, next))}");
    }

    public static TheoryData<string, string, string> BackwardIncompatibleChanges => new()
    {
        { "requiring a new property", """{ "type": "object", "properties": { "id": { "type": "string" }, "total": { "type": "number", "minimum": 0 } }, "required": [ "id", "total" ] }""", "'total' is required" },
        { "narrowing a type", """{ "type": "object", "properties": { "id": { "type": "string" }, "total": { "type": "integer", "minimum": 0 } }, "required": [ "id" ] }""", "type 'number' is no longer accepted" },
        { "tightening a bound", """{ "type": "object", "properties": { "id": { "type": "string" }, "total": { "type": "number", "minimum": 10 } }, "required": [ "id" ] }""", "'minimum' has been raised to 10" },
        { "forbidding additional properties", """{ "type": "object", "properties": { "id": { "type": "string" }, "total": { "type": "number", "minimum": 0 } }, "required": [ "id" ], "additionalProperties": false }""", "additional properties are no longer allowed" },
        { "constraining a property with a pattern", """{ "type": "object", "properties": { "id": { "type": "string", "pattern": "^o-" }, "total": { "type": "number", "minimum": 0 } }, "required": [ "id" ] }""", "'pattern' constraint has changed" },
        { "adding a property to a schema that allows any other", """{ "type": "object", "properties": { "id": { "type": "string" }, "total": { "type": "number", "minimum": 0 }, "notes": { "type": "string" } }, "required": [ "id" ] }""", "at '/properties/notes' the type is restricted" },
        { "restricting values to an enumeration", """{ "type": "object", "properties": { "id": { "type": "string", "enum": [ "a", "b" ] }, "total": { "type": "number", "minimum": 0 } }, "required": [ "id" ] }""", "restricted to an enumeration" }
    };

    [Theory]
    [MemberData(nameof(BackwardIncompatibleChanges))]
    public void Backward_Incompatible_Changes_Should_Be_Refused_With_The_Reason(string change, string next, string reason)
    {
        var errors = Check(Order, next);
        Assert.True(errors.Count > 0, $"Expected {change} to be backward incompatible");
        Assert.Contains(errors, e => e.Contains(reason));
    }

    [Fact]
    public void Adding_A_Property_A_Closed_Schema_Did_Not_Allow_Should_Be_Backward_Compatible()
    {
        // the previous version never accepted the property: constraining it refuses nothing the previous version accepted
        const string closed = """{ "type": "object", "properties": { "id": { "type": "string" } }, "additionalProperties": false }""";
        Assert.Empty(Check(closed, """{ "type": "object", "properties": { "id": { "type": "string" }, "total": { "type": "number" } }, "additionalProperties": false }"""));
    }

    [Fact]
    public void Requiring_A_New_Property_Should_Be_Forward_But_Not_Backward_Compatible()
    {
        const string next = """{ "type": "object", "properties": { "id": { "type": "string" }, "total": { "type": "number", "minimum": 0 } }, "required": [ "id", "total" ] }""";
        Assert.NotEmpty(Check(Order, next, SchemaCompatibility.Backward));
        Assert.Empty(Check(Order, next, SchemaCompatibility.Forward));
        Assert.NotEmpty(Check(Order, next, SchemaCompatibility.Full));
        Assert.Empty(Check(Order, next, SchemaCompatibility.None));
    }

    [Fact]
    public void Nested_Changes_Should_Be_Checked()
    {
        const string previous = """{ "type": "object", "properties": { "lines": { "type": "array", "items": { "type": "object", "properties": { "sku": { "type": "string" } }, "required": [ "sku" ] } } } }""";
        const string next = """{ "type": "object", "properties": { "lines": { "type": "array", "items": { "type": "object", "properties": { "sku": { "type": "string" }, "quantity": { "type": "integer" } }, "required": [ "sku", "quantity" ] } } } }""";
        Assert.Contains(Check(previous, next), e => e.Contains("/properties/lines/items") && e.Contains("'quantity' is required"));
    }

    static Schema Schema(string compatibility, params (int Version, string Json)[] versions) => new(new("orders"), new()
    {
        Compatibility = compatibility,
        Versions = [.. versions.Select(v => new SchemaVersion() { Version = v.Version, Schema = Json(v.Json) })]
    });

    static readonly SchemaValidator Validator = new(Neuroglia.Serialization.Json.JsonSerializer.Default);

    const string WithNotes = """{ "type": "object", "properties": { "id": { "type": "string" }, "total": { "type": "number", "minimum": 0 }, "notes": { "type": "string" } }, "required": [ "id" ] }""";

    const string Optional = """{ "type": "object", "properties": { "id": { "type": "string" }, "total": { "type": "number", "minimum": 0 } } }""";

    const string RequiringTotal = """{ "type": "object", "properties": { "id": { "type": "string" }, "total": { "type": "number", "minimum": 0 } }, "required": [ "id", "total" ] }""";

    [Fact]
    public void A_Schema_Whose_Versions_Are_Compatible_Should_Be_Valid()
    {
        Assert.True(Validator.Validate(Schema(SchemaCompatibility.Backward, (1, Order), (2, Optional))).IsValid);
    }

    [Fact]
    public void A_New_Incompatible_Version_Should_Be_Refused()
    {
        // arrange
        var original = Schema(SchemaCompatibility.Backward, (1, Order));

        // act
        var result = Validator.Validate(Schema(SchemaCompatibility.Backward, (1, Order), (2, RequiringTotal)), original);

        // assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("Version 2 is not backward compatible with version 1"));
    }

    [Fact]
    public void Existing_Versions_Should_Neither_Be_Changed_Nor_Removed()
    {
        var original = Schema(SchemaCompatibility.Backward, (1, Order), (2, WithNotes));
        Assert.Contains(Validator.Validate(Schema(SchemaCompatibility.Backward, (1, WithNotes), (2, WithNotes)), original).Errors, e => e.ErrorMessage.Contains("Version 1 cannot be changed"));
        Assert.Contains(Validator.Validate(Schema(SchemaCompatibility.Backward, (2, WithNotes)), original).Errors, e => e.ErrorMessage.Contains("Version 1 cannot be removed"));
    }

    [Fact]
    public void Versions_That_Have_Been_Accepted_Should_Not_Be_Checked_Again()
    {
        // arrange - the compatibility is tightened after incompatible versions have been accepted
        var original = Schema(SchemaCompatibility.None, (1, Order), (2, RequiringTotal));

        // act
        var result = Validator.Validate(Schema(SchemaCompatibility.Backward, (1, Order), (2, RequiringTotal)), original);

        // assert
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("spec.versions", "versions")]
    [InlineData("spec.versions[1].version", "decreasing")]
    [InlineData("spec.versions[0].schema", "not a schema")]
    [InlineData("spec.compatibility", "compatibility")]
    public void Invalid_Schemas_Should_Be_Refused(string property, string invalidity)
    {
        var schema = invalidity switch
        {
            "versions" => Schema(SchemaCompatibility.Backward),
            "decreasing" => Schema(SchemaCompatibility.None, (2, Order), (1, Order)),
            "not a schema" => Schema(SchemaCompatibility.Backward, (1, "\"order\"")),
            _ => Schema("transitive", (1, Order))
        };
        Assert.Contains(Validator.Validate(schema).Errors, e => e.PropertyName == property);
    }

}
