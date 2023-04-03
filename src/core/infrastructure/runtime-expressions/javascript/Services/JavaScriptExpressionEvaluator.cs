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
    public object? Evaluate(string expression, object input, IDictionary<string, object>? arguments = null, Type? expectedType = null)
    {
        if (string.IsNullOrWhiteSpace(expression)) throw new ArgumentNullException(nameof(expression));
        if (input == null) throw new ArgumentNullException(nameof(input));
        if (expectedType == null) expectedType = typeof(object);
        expression = expression.Trim();
        if (string.IsNullOrWhiteSpace(expression)) throw new ArgumentNullException(nameof(expression));
        var jsEngine = new Engine(options =>
        {
            // Limit memory allocations to 1MB
            options.LimitMemory(1_000_000)
            // Set a timeout to 500ms
                .TimeoutInterval(TimeSpan.FromMicroseconds(500))
            // Set limit of 500 executed statements
                .MaxStatements(500)
            // Set limit of 16 for recursive calls
                .LimitRecursion(16)
            // Use a cancellation token.
                //.CancellationToken(cancellationToken)
            ;
        });
        jsEngine.SetValue("input", input);
        if (arguments != null)
        {
            foreach (var argument in arguments)
            {
                jsEngine.SetValue(argument.Key, argument.Value);
            }
        }
        return jsEngine.Evaluate(expression).UnwrapIfPromise().ToObject();
    }
}
