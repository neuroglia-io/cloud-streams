// Copyright © 2023-Present The Cloud Streams Authors
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
using CloudStreams.Core.Infrastructure;
using CloudStreams.Core.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Newtonsoft.Json.Linq;
using System.Dynamic;
using System.Text.Json;
using System.Xml.Linq;

namespace CloudStreams.UnitTests.Cases.Core.RuntimeExpressions;

public class JQExpressionEvaluatorTests
{
    [Fact]
    public void Evaluate_PrimitiveOutput_ShouldWork()
    {
        //arrange
        var evaluator = BuildExpressionEvaluator();
        var value = 42;
        var expression = "${ .value }";
        var data = new { value };

        //act
        var result = evaluator.Evaluate<int>(expression, data);

        //assert
        result.Should().Be(value);
    }

    [Fact]
    public void Evaluate_ComplexTypeOutput_ShouldWork()
    {
        //arrange
        var evaluator = BuildExpressionEvaluator();
        var value = 42;
        var expression = "${ . }";
        var data = new { value };
        var expected = data.ToDictionary<int>()!;

        //act
        var result = evaluator.Evaluate<Dictionary<string,int>>(expression, data)!;

        //assert
        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void Evaluate_Object_ShouldWork()
    {
        //arrange
        var evaluator = BuildExpressionEvaluator();
        var expression = "{ foo: \"bar\", fizz: \"buzz\" }";
        var data = new { foo = "bar", fizz = "buzz" };
        var expected = data.ToDictionary<string>()!;

        //act
        var result = evaluator.Evaluate(expression, data).ToDictionary<string>()!;

        //assert
        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void Evaluate_LargeData_ShouldWork()
    {
        //arrange
        var evaluator = BuildExpressionEvaluator();
        var data = Serializer.Json.Deserialize<List<ExpandoObject>>(File.ReadAllText(Path.Combine("Assets", "ExpressionEvaluation", "dogs.json")))!;
        var expression = ". | map(select(.category.name == $CONST.category))[0]";
        var args = new Dictionary<string, object>() { { "CONST", new { category = "Pugal" } } };

        //act
        var result = evaluator.Evaluate(expression, data, args);

        //assert
        result.Should().NotBeNull();
    }

    [Fact]
    public void Evaluate_LargeExpression_ShouldWork()
    {
        //arrange
        var evaluator = BuildExpressionEvaluator();
        var data = new { };
        var expression = File.ReadAllText(Path.Combine("Assets", "ExpressionEvaluation", "pets.expression.jq.txt"))!;
        var args = new Dictionary<string, object>() { { "CONST", new { category = "Pugal" } } };

        //act
        dynamic result = evaluator.Evaluate(expression, data, args)!;
        int petsLength = result.GetProperty("pets").GetArrayLength();

        //assert
        petsLength.Should().Be(425);
    }

    [Fact]
    public void Evaluate_EscapedJsonInput_ShouldWork()
    {
        //arrange
        var evaluator = BuildExpressionEvaluator();
        var json = File.ReadAllText(Path.Combine("Assets", "ExpressionEvaluation", "input-with-escaped-json.json"));
        var data = Serializer.Json.Deserialize<ExpandoObject>(json)!;
        var expression = "${ ._user }";

        //act
        dynamic result = evaluator.Evaluate(expression, data)!;
        string name  = result.GetProperty("name").GetString();

        //assert
        name.Should().Be("fake-user-name");
    }

    [Fact]
    public void Evaluate_String_Concatenation_ShouldWork()
    {
        //arrange
        var evaluator = BuildExpressionEvaluator();
        var data = Serializer.Json.Deserialize<ExpandoObject>(File.ReadAllText(Path.Combine("Assets", "ExpressionEvaluation", "string-concat.input.json")))!;
        var expression = File.ReadAllText(Path.Combine("Assets", "ExpressionEvaluation", "string-concat.expression.jq.txt"));

        //act
        string result = (string)evaluator.Evaluate(expression, data, expectedType: typeof(string))!;

        //assert
        result.Should().Be("hello world");
    }

    [Fact]
    public void Evaluate_String_Interpolation_ShouldWork()
    {
        //arrange
        var evaluator = BuildExpressionEvaluator();
        var data = Serializer.Json.Deserialize<ExpandoObject>(File.ReadAllText(Path.Combine("Assets", "ExpressionEvaluation", "string-interpolation.input.json")))!;
        var expression = File.ReadAllText(Path.Combine("Assets", "ExpressionEvaluation", "string-interpolation.expression.jq.txt"));

        //act
        string result = (string)evaluator.Evaluate(expression, data, expectedType: typeof(string))!;

        //assert
        result.Should().Be("hello world is a greeting");
    }

    [Fact]
    public void Evaluate_Complex_String_Substitution_ShouldWork()
    {
        //arrange
        var evaluator = BuildExpressionEvaluator();
        var data = Serializer.Json.Deserialize<ExpandoObject>(File.ReadAllText(Path.Combine("Assets", "ExpressionEvaluation", "string-substitution.input.json")))!;
        var expression = File.ReadAllText(Path.Combine("Assets", "ExpressionEvaluation", "string-substitution.expression.jq.txt"));

        //act
        string result = (string)evaluator.Evaluate(expression, data, expectedType: typeof(string))!;

        //assert
        result.Should().Be("Hello world");
    }

    [Fact]
    public void Evaluate_String_With_Escaped_Quotes_ShouldWork()
    {
        //arrange
        var evaluator = BuildExpressionEvaluator();
        var data = Serializer.Json.Deserialize<ExpandoObject>(File.ReadAllText(Path.Combine("Assets", "ExpressionEvaluation", "string-quoted.input.json")))!;
        var expression = File.ReadAllText(Path.Combine("Assets", "ExpressionEvaluation", "string-quoted.expression.jq.txt"));

        //act
        string result = (string)evaluator.Evaluate(expression, data, expectedType: typeof(string))!;

        //assert
        result.Should().Be(@"bar is ""bar""");
    }

    static IExpressionEvaluator BuildExpressionEvaluator()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.TryAddSingleton<IExpressionEvaluator, JQExpressionEvaluator>();
        return services.BuildServiceProvider().GetRequiredService<IExpressionEvaluator>();
    }
}
