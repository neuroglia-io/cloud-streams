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
using System.Dynamic;

namespace CloudStreams.UnitTests.Cases.Core.RuntimeExpressions;

internal class DataHolder
{
    public int Value { get; set;}
}

public class JavaScriptExpressionEvaluatorTests
{

    [Fact]
    public void Evaluate_PrimitiveOutput_ShouldWork()
    {
        //arrange
        var evaluator = BuildExpressionEvaluator();
        var value = 42;
        var data = new { value };
        var expression = "input.value";

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
        var data = new { value };
        var expression = "${ input }";
        var expected = data.ToDictionary<int>()!;

        //act
        var result = evaluator.Evaluate<ExpandoObject>(expression, data).ToDictionary<int>()!;

        //assert
        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void Evaluate_Object_ShouldWork()
    {
        //arrange
        var evaluator = BuildExpressionEvaluator();
        var data = new { foo = "bar", fizz = "buzz" };
        var expression = "({ foo: 'bar', fizz: 'buzz' })";
        var expected = data.ToDictionary<string>()!;

        //act
        var result = evaluator.Evaluate(expression, data).ToDictionary<string>()!;
        
        //assert
        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void Evaluate_ShouldNotMutate()
    {
        //arrange
        var evaluator = BuildExpressionEvaluator();
        var value = 42;
        var data = new DataHolder { Value = value };
        var expression = "input.Value = 24; input.Value";

        //act
        var result = evaluator.Evaluate<int>(expression, data);

        //assert
        data.Should().BeEquivalentTo(new { Value = 42 });
        result.Should().Be(24);
    }

    [Fact]
    public void Evaluate_LargeData_ShouldWork()
    {
        //arrange
        var evaluator = BuildExpressionEvaluator();
        // TODO: remove Newtonsoft dependency
        var data = Newtonsoft.Json.JsonConvert.DeserializeObject<List<ExpandoObject>>(File.ReadAllText(Path.Combine("Assets", "ExpressionEvaluation", "dogs.json")))!;
        var args = new Dictionary<string, object>() { { "CONST", new { category = "Pugal" } } };
        var expression = "input.filter(i => i.category?.name === CONST.category)[0]";

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
        var args = new Dictionary<string, object>() { { "CONST", new { category = "Pugal" } } };
        var expression = File.ReadAllText(Path.Combine("Assets", "ExpressionEvaluation", "pets.expression.js.txt"));

        //act
        dynamic result = evaluator.Evaluate(expression, data, args)!;
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
        // TODO: remove Newtonsoft dependency
        var data = Newtonsoft.Json.JsonConvert.DeserializeObject<ExpandoObject>(json)!;
        var expression = "input._user";

        //act
        dynamic result = evaluator.Evaluate(expression, data)!;
        string name = result.name;

        //assert
        name.Should().Be("fake-user-name");
    }

    [Fact]
    public void Evaluate_String_Concatenation_ShouldWork()
    {
        //arrange
        var evaluator = BuildExpressionEvaluator();
        // TODO: remove Newtonsoft dependency
        var data = Newtonsoft.Json.JsonConvert.DeserializeObject<ExpandoObject>(File.ReadAllText(Path.Combine("Assets", "ExpressionEvaluation", "string-concat.input.json")))!;
        var expression = File.ReadAllText(Path.Combine("Assets", "ExpressionEvaluation", "string-concat.expression.js.txt"));

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
        // TODO: remove Newtonsoft dependency
        var data = Newtonsoft.Json.JsonConvert.DeserializeObject<ExpandoObject>(File.ReadAllText(Path.Combine("Assets", "ExpressionEvaluation", "string-interpolation.input.json")))!;
        var expression = File.ReadAllText(Path.Combine("Assets", "ExpressionEvaluation", "string-interpolation.expression.js.txt"));

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
        // TODO: remove Newtonsoft dependency
        var data = Newtonsoft.Json.JsonConvert.DeserializeObject<ExpandoObject>(File.ReadAllText(Path.Combine("Assets", "ExpressionEvaluation", "string-substitution.input.json")))!;
        var expression = File.ReadAllText(Path.Combine("Assets", "ExpressionEvaluation", "string-substitution.expression.js.txt"));

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
        // TODO: remove Newtonsoft dependency
        var data = Newtonsoft.Json.JsonConvert.DeserializeObject<ExpandoObject>(File.ReadAllText(Path.Combine("Assets", "ExpressionEvaluation", "string-quoted.input.json")))!;
        var expression = File.ReadAllText(Path.Combine("Assets", "ExpressionEvaluation", "string-quoted.expression.js.txt"));

        //act
        string result = (string)evaluator.Evaluate(expression, data, expectedType: typeof(string))!;

        //assert
        result.Should().Be(@"bar is ""bar""");
    }

    static IExpressionEvaluator BuildExpressionEvaluator()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.TryAddSingleton<IExpressionEvaluator, JavaScriptExpressionEvaluator>();
        return services.BuildServiceProvider().GetRequiredService<IExpressionEvaluator>();
    }
}
