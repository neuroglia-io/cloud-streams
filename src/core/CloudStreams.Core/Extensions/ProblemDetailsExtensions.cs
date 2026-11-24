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

using System.Net;

namespace CloudStreams.Core;

/// <summary>
/// Defines extensions for <see cref="ProblemDetails"/>
/// </summary>
public static class ProblemDetailsExtensions
{

    /// <summary>
    /// Normalizes the <see cref="ProblemDetails"/>, so that its status describes the problem
    /// </summary>
    /// <param name="problem">The <see cref="ProblemDetails"/> to normalize</param>
    /// <returns>The normalized <see cref="ProblemDetails"/></returns>
    /// <remarks>
    /// The resource repository reports rejected admissions with a 404 status, as if the resource did not exist. They are invalid requests, and are reported as such
    /// </remarks>
    public static ProblemDetails Normalize(this ProblemDetails problem)
    {
        if (problem == null || problem.Status != (int)HttpStatusCode.NotFound || problem.Type?.OriginalString.EndsWith("/admission-failed", StringComparison.OrdinalIgnoreCase) != true) return problem!;
        return new(problem.Type, problem.Title!, (int)HttpStatusCode.BadRequest, problem.Detail, problem.Instance, problem.Errors);
    }

}
