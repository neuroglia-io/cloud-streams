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

using CloudStreams.Gateway.Application.Services;
using System.Text.Json.Nodes;

namespace CloudStreams.UnitTests.Cases.Gateway;

public class JsonSchemaGeneratorTests
{

    readonly JsonSchemaGenerator _generator = new(Neuroglia.Serialization.Json.JsonSerializer.Default);

    [Theory]
    [InlineData("[]")]
    [InlineData("[null, null]")]
    [InlineData("{ \"items\": [] }")]
    public async Task Generating_A_Schema_For_Arrays_Without_Items_Should_Not_Throw(string json)
    {
        // act
        var schema = await this._generator.GenerateAsync(JsonNode.Parse(json));

        // assert
        Assert.NotNull(schema);
    }

    [Fact]
    public async Task Generating_A_Schema_For_An_Array_Should_Describe_Its_Items()
    {
        // act
        var schema = await this._generator.GenerateAsync(JsonNode.Parse("[1, 2]"));

        // assert
        Assert.NotNull(schema);
        Assert.Contains("\"items\"", Neuroglia.Serialization.Json.JsonSerializer.Default.SerializeToText(schema));
    }

}
