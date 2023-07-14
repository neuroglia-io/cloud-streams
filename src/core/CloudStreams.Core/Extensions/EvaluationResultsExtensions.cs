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

using Json.Schema;

namespace CloudStreams.Core;

/// <summary>
/// Defines extensions for <see cref="EvaluationResults"/>
/// </summary>
public static class EvaluationResultsExtensions
{

    /// <summary>
    /// Converts the <see cref="EvaluationResults"/> to an <see cref="IEnumerable{T}"/> of errors, if any
    /// </summary>
    /// <param name="evaluationResults">The <see cref="EvaluationResults"/> to convert</param>
    /// <returns>A new <see cref="IEnumerable{T}"/> containing the <see cref="EvaluationResults"/>'s errors, if any</returns>
    public static IEnumerable<KeyValuePair<string, string[]>>? ToErrorList(this EvaluationResults evaluationResults)
    {
        if (evaluationResults.IsValid) return null;
        var errors = new List<KeyValuePair<string, string[]>>();
        if (evaluationResults.Errors?.Any() == true) errors = evaluationResults.Errors.Select(e => new KeyValuePair<string, string[]>(evaluationResults.InstanceLocation.ToString(), new string[] { e.Value })).ToList();
        if (!evaluationResults.Details.Any()) return null;
        foreach (var detail in evaluationResults.Details)
        {
            var childErrors = detail.ToErrorList();
            if (childErrors == null) continue;
            foreach (var error in childErrors)
            {
                errors.Add(error);
            }
        }
        return errors;
    }

}