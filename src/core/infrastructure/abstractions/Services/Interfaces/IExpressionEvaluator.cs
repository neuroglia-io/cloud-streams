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

namespace CloudStreams.Core.Infrastructure.Services;

/// <summary>
/// Defines the fundamentals of a service used to evaluate runtime expressions
/// </summary>
public interface IExpressionEvaluator
{

    /// <summary>
    /// Evaluates the specified runtime expression
    /// </summary>
    /// <param name="expression">The runtime expression to evaluate</param>
    /// <param name="input">The data to evaluate the runtime expression against</param>
    /// <param name="arguments">A key/value mapping of the arguments used during evaluation, if any</param>
    /// <param name="expectedType">The expected type of the evaluation's result</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The evaluation's result</returns>
    object? Evaluate(string expression, object input, IDictionary<string, object>? arguments = null, Type? expectedType = null, CancellationToken cancellationToken = default);

}