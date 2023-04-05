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

using Jint;
using Jint.Native;
using Jint.Runtime.Interop;
using Microsoft.VisualBasic;
using System.Reflection;
using System.Text.Json;

namespace CloudStreams.Core.Infrastructure.Services;

/// <summary>
/// Represents the JavaScript implementation of the <see cref="IExpressionEvaluator"/> interface
/// </summary>
public class JavaScriptExpressionEvaluator
    : IExpressionEvaluator
{
    /// <summary>
    /// Instanciates a new <see cref="JavaScriptExpressionEvaluator"/>
    /// </summary>
    public JavaScriptExpressionEvaluator() { }

    /// <inheritdoc/>
    public object? Evaluate(string expression, object input, IDictionary<string, object>? arguments = null, Type? expectedType = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(expression)) throw new ArgumentNullException(nameof(expression));
        if (input == null) throw new ArgumentNullException(nameof(input));
        if (expectedType == null) expectedType = typeof(object);
        expression = expression.Trim();
        if (expression.StartsWith("${"))
            expression = expression[2..^1].Trim();
        if (string.IsNullOrWhiteSpace(expression)) throw new ArgumentNullException(nameof(expression));
        var jsEngine = new Engine(options =>
        {
            // Limit memory allocations to 1MB
            options.LimitMemory(10_000_000)
                // Set a timeout to 500ms
                .TimeoutInterval(TimeSpan.FromMilliseconds(500))
                // Set limit of 500 executed statements
                .MaxStatements(500)
                // Set limit of 16 for recursive calls
                .LimitRecursion(16)
                // Use a cancellation token.
                .CancellationToken(cancellationToken)
                // customizing object wrapping to set array prototype to objects
                .SetWrapObjectHandler((engine, target) =>
                {
                    var instance = new ObjectWrapper(engine, target);
                    if (instance.IsArrayLike)
                    {
                        instance.SetPrototypeOf(engine.Realm.Intrinsics.Array.PrototypeObject);
                    }
                    return instance;
                })
            ;
        });
        this.CopyValue(jsEngine, "input", input);
        if (arguments != null && arguments.Any())
        {
            foreach (var argument in arguments)
            {
                this.CopyValue(jsEngine, argument.Key, argument.Value);
            }
        }
        var result = jsEngine.Evaluate(expression).UnwrapIfPromise().ToObject();
        jsEngine.Dispose();
        if (expectedType == typeof(object))
            return result;
        return Serializer.Json.Deserialize(Serializer.Json.Serialize(result), expectedType);
    }

    /// <summary>
    /// Clones the provided value
    /// </summary>
    /// <typeparam name="T">The type of the value to clone</typeparam>
    /// <param name="value">The seed value</param>
    /// <returns>The cloned value</returns>
    /* TODO: Should be prefered to the "CopyValue" method because it will use less memory
     * but System.Text creates unwanted side effects by wrapping the content in JsonElement
     */
    private T Clone<T>(T value)
    {
        if (value == null)
        {
            return default(T)!;
        }
        if (value.GetType().IsPrimitive) { 
            return value; 
        }
        // System.Text introduces side effects with its "JsonElements"
        return Serializer.Json.Deserialize<T>(Serializer.Json.Serialize(value))!;
        /* Newtonsoft works but is banned from the project
        return Newtonsoft.Json.JsonConvert.DeserializeObject<T>(Newtonsoft.Json.JsonConvert.SerializeObject(value))!;
        */
    }

    /// <summary>
    /// Copies the provided value to a variable inside the engine under the provided name. 
    /// It prevents the JS engine from being able to mutate the inputed value out of its execution context.
    /// </summary>
    /// <param name="engine">The instance of <see cref="Engine"/> to set the variable in</param>
    /// <param name="name">The name of the variable</param>
    /// <param name="value">The value of the variable</param>
    private void CopyValue(Engine engine, string name, object value)
    {
        if (value == null || value.GetType().IsPrimitive)
        {
            engine.SetValue(name, value);
        }
        engine.SetValue($"__{name}__buffer", value);
        engine.Execute($"const {name} = JSON.parse(JSON.stringify(__{name}__buffer)); delete __{name}__buffer;");
    }
}
