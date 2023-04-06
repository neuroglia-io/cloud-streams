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
using CloudStreams.Core.Data.Models;
using CloudStreams.Core.Infrastructure;
using CloudStreams.Core.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Dynamic;

namespace CloudStreams.UnitTests.Cases.Core.RuntimeExpressions;

public class CSharpExpressionEvaluatorTests
{
    [Fact]
    public void Evaluate_KnwonType_PrimitiveOutput_ShouldWork()
    {
        //arrange
        var evaluator = BuildExpressionEvaluator();
        var input = BuildMockCloudEvent();
        var expression = "input.Type";

        //act
        var result = evaluator.Evaluate<string>(expression, input);

        //assert
        result.Should().Be(input.Type);
    }

    [Fact]
    public void Evaluate_KnwonType_MethodOutput_ShouldWork()
    {
        //arrange
        var evaluator = BuildExpressionEvaluator();
        var input = BuildMockCloudEvent();
        var expression = "input.GetAttribute(\"Sequence\")";

        //act
        var result = evaluator.Evaluate<int>(expression, input);

        //assert
        result.Should().Be(42);
    }

    [Fact]
    public void Evaluate_KnwonType_InnerAnonymousOutput_ShouldWork()
    {
        //arrange
        var evaluator = BuildExpressionEvaluator();
        var input = BuildMockCloudEvent();
        var expression = "input.Data.Id";

        //act
        var result = evaluator.Evaluate<string>(expression, input);

        //assert
        result.Should().Be("");
    }

    [Fact]
    public void Evaluate_UnknwonType_PrimitiveOutput_ShouldWork()
    {
        //arrange
        var evaluator = BuildExpressionEvaluator();
        var value = 42;
        var input = new { value };
        var expression = "input.value";

        //act
        var result = evaluator.Evaluate<int>(expression, input);

        //assert
        result.Should().Be(value);
    }

    [Fact]
    public void Evaluate_ComplexTypeOutput_ShouldWork()
    {
        //arrange
        var evaluator = BuildExpressionEvaluator();
        var value = 42;
        var input = new { value };
        var expression = "${ input }";
        var expected = input.ToDictionary<int>()!;

        //act
        var result = evaluator.Evaluate<ExpandoObject>(expression, input).ToDictionary<int>()!;

        //assert
        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void Evaluate_Object_ShouldWork()
    {
        //arrange
        var evaluator = BuildExpressionEvaluator();
        var input = new { foo = "bar", fizz = "buzz" };
        var expression = "({ foo: 'bar', fizz: 'buzz' })";
        var expected = input.ToDictionary<string>()!;

        //act
        var result = evaluator.Evaluate(expression, input).ToDictionary<string>()!;

        //assert
        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void Evaluate_ShouldNotMutate()
    {
        //arrange
        var evaluator = BuildExpressionEvaluator();
        var value = 42;
        var input = new ValueHolder { Value = value };
        var expression = """
            new Lazy<int>(() =>
            {
                input.Value = 24;
                return input.Value;
            }).Value
            """;

        //act
        var result = evaluator.Evaluate<int>(expression, input);

        //assert
        input.Should().BeEquivalentTo(new { Value = 42 });
        result.Should().Be(24);
    }

    [Fact]
    public void Evaluate_LargeData_ShouldWork()
    {
        //arrange
        var evaluator = BuildExpressionEvaluator();
        var input = Serializer.Json.Deserialize<List<ExpandoObject>>(File.ReadAllText(Path.Combine("Assets", "ExpressionEvaluation", "dogs.json")))!;
        var args = new Dictionary<string, object>() { { "CONST", new { category = "Pugal" } } };
        var expression = "input.filter(i => i.category?.name === CONST.category)[0]";

        //act
        var result = evaluator.Evaluate(expression, input, args);

        //assert
        result.Should().NotBeNull();
    }

    [Fact]
    public void Evaluate_LargeExpression_ShouldWork()
    {
        //arrange
        var evaluator = BuildExpressionEvaluator();
        var input = new { };
        var args = new Dictionary<string, object>() { { "CONST", new { category = "Pugal" } } };
        var expression = File.ReadAllText(Path.Combine("Assets", "ExpressionEvaluation", "pets.expression.cs.txt"));

        //act
        dynamic result = evaluator.Evaluate(expression, input, args)!;
        int petsLength = ((IEnumerable<object>)result.pets).Count();

        //assert
        petsLength.Should().Be(425);
    }

    [Fact]
    public void Evaluate_EscapedJsonInput_ShouldWork()
    {
        //arrange
        var evaluator = BuildExpressionEvaluator();
        var json = File.ReadAllText(Path.Combine("Assets", "ExpressionEvaluation", "input-with-escaped-json.json"));
        var input = Serializer.Json.Deserialize<ExpandoObject>(json)!;
        var expression = "input._user";

        //act
        dynamic result = evaluator.Evaluate(expression, input)!;
        string name = result.name;

        //assert
        name.Should().Be("fake-user-name");
    }

    [Fact]
    public void Evaluate_String_Concatenation_ShouldWork()
    {
        //arrange
        var evaluator = BuildExpressionEvaluator();
        var input = Serializer.Json.Deserialize<ExpandoObject>(File.ReadAllText(Path.Combine("Assets", "ExpressionEvaluation", "string-concat.input.json")))!;
        var expression = File.ReadAllText(Path.Combine("Assets", "ExpressionEvaluation", "string-concat.expression.cs.txt"));

        //act
        string result = (string)evaluator.Evaluate(expression, input, expectedType: typeof(string))!;

        //assert
        result.Should().Be("hello world");
    }

    [Fact]
    public void Evaluate_String_Interpolation_ShouldWork()
    {
        //arrange
        var evaluator = BuildExpressionEvaluator();
        var input = Serializer.Json.Deserialize<ExpandoObject>(File.ReadAllText(Path.Combine("Assets", "ExpressionEvaluation", "string-interpolation.input.json")))!;
        var expression = File.ReadAllText(Path.Combine("Assets", "ExpressionEvaluation", "string-interpolation.expression.cs.txt"));

        //act
        string result = (string)evaluator.Evaluate(expression, input, expectedType: typeof(string))!;

        //assert
        result.Should().Be("hello world is a greeting");
    }

    [Fact]
    public void Evaluate_Complex_String_Substitution_ShouldWork()
    {
        //arrange
        var evaluator = BuildExpressionEvaluator();
        var input = Serializer.Json.Deserialize<ExpandoObject>(File.ReadAllText(Path.Combine("Assets", "ExpressionEvaluation", "string-substitution.input.json")))!;
        var expression = File.ReadAllText(Path.Combine("Assets", "ExpressionEvaluation", "string-substitution.expression.cs.txt"));

        //act
        string result = (string)evaluator.Evaluate(expression, input, expectedType: typeof(string))!;

        //assert
        result.Should().Be("Hello world");
    }

    [Fact]
    public void Evaluate_String_With_Escaped_Quotes_ShouldWork()
    {
        //arrange
        var evaluator = BuildExpressionEvaluator();
        var input = Serializer.Json.Deserialize<ExpandoObject>(File.ReadAllText(Path.Combine("Assets", "ExpressionEvaluation", "string-quoted.input.json")))!;
        var expression = File.ReadAllText(Path.Combine("Assets", "ExpressionEvaluation", "string-quoted.expression.cs.txt"));

        //act
        string result = (string)evaluator.Evaluate(expression, input, expectedType: typeof(string))!;

        //assert
        result.Should().Be(@"bar is ""bar""");
    }

    static IExpressionEvaluator BuildExpressionEvaluator()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.TryAddSingleton<IExpressionEvaluator, CSharpExpressionEvaluator>();
        return services.BuildServiceProvider().GetRequiredService<IExpressionEvaluator>();
    }

    static CloudEvent BuildMockCloudEvent()
    {
        return new CloudEvent()
        {
            SpecVersion = "1.0",
            Time = DateTimeOffset.Now,
            Id = "577d3bed-77c4-4d75-9d1c-4bfe5994828b",
            Type = "my-event-type",
            Source = new Uri("https://my-event.source.com"),
            Subject = "the-subject",
            DataContentType = "application/json",
            Data = new
            {
                Id = "user-123",
                Action = "logged-in"
            },
            ExtensionAttributes = new Dictionary<string, object>
            {
                { "Sequence", 42 }
            }
        };
    }
}
