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

using CloudStreams.Core.Infrastructure.Services.InternalExtensions;
using Jint;
using Jint.Runtime.Interop;

namespace CloudStreams.Core.Infrastructure.Services;

/// <summary>
/// Represents the JavaScript implementation of the <see cref="IExpressionEvaluator"/> interface
/// </summary>
public class JavaScriptExpressionEvaluator
    : IExpressionEvaluator
{

    /// <inheritdoc/>
    public object? Evaluate(string expression, object input, IDictionary<string, object>? arguments = null, Type? expectedType = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(expression)) throw new ArgumentNullException(nameof(expression));
        if (input == null) throw new ArgumentNullException(nameof(input));
        if (expectedType == null) expectedType = typeof(object);
        expression = expression.Trim();
        if (expression.StartsWith("${")) expression = expression[2..^1].Trim();
        if (string.IsNullOrWhiteSpace(expression)) throw new ArgumentNullException(nameof(expression));
        var jsEngine = new Engine(options =>
        {
            // Limit memory allocations to 1MB
            options.LimitMemory(1_000_000)
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
                    if (instance.IsArrayLike) instance.SetPrototypeOf(engine.Realm.Intrinsics.Array.PrototypeObject);
                    return instance;
                })
            ;
        });
        jsEngine.SetValue("input", input.Copy());
        if (arguments != null && arguments.Any())
        {
            foreach (var argument in arguments)
            {
                jsEngine.SetValue(argument.Key, argument.Value.Copy());
            }
        }
        var result = jsEngine.Evaluate(expression).UnwrapIfPromise().ToObject();
        jsEngine.Dispose();
        if (expectedType == typeof(object)) return result;
        return Hylo.Serializer.Json.Deserialize(Hylo.Serializer.Json.Serialize(result), expectedType);
    }
}
