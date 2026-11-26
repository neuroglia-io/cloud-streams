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
/// <remarks>
/// The type and the title of problems and errors are optional when deserialized, but required to create them: a missing one is replaced by a default derived
/// from the status
/// </remarks>
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
        return new(problem.Type, problem.Title ?? GetDefaultTitle((int)HttpStatusCode.BadRequest), (int)HttpStatusCode.BadRequest, problem.Detail, problem.Instance, problem.Errors);
    }

    /// <summary>
    /// Gets the type of a problem of the specified status, used when a problem or an error does not have one
    /// </summary>
    /// <param name="status">The status of the problem</param>
    /// <returns>The default type of a problem of the specified status</returns>
    public static Uri GetDefaultType(int status) => new($"https://httpstatuses.io/{status}");

    /// <summary>
    /// Gets the title of a problem of the specified status, used when a problem or an error does not have one
    /// </summary>
    /// <param name="status">The status of the problem</param>
    /// <returns>The default title of a problem of the specified status</returns>
    public static string GetDefaultTitle(int status) => Enum.IsDefined(typeof(HttpStatusCode), status) ? ((HttpStatusCode)status).ToString() : $"Status {status}";

    /// <summary>
    /// Converts the <see cref="ProblemDetails"/> into a new <see cref="Neuroglia.Error"/>
    /// </summary>
    /// <param name="problem">The <see cref="ProblemDetails"/> to convert</param>
    /// <returns>A new <see cref="Neuroglia.Error"/> that describes the problem</returns>
    public static Error ToError(this ProblemDetails problem)
    {
        ArgumentNullException.ThrowIfNull(problem);
        return new(problem.Type ?? GetDefaultType(problem.Status), problem.Title ?? GetDefaultTitle(problem.Status), problem.Status, problem.Detail, problem.Instance, problem.Errors);
    }

    /// <summary>
    /// Converts the <see cref="Neuroglia.Error"/> into new <see cref="ProblemDetails"/>
    /// </summary>
    /// <param name="error">The <see cref="Neuroglia.Error"/> to convert</param>
    /// <returns>New <see cref="ProblemDetails"/> that describe the error</returns>
    public static ProblemDetails ToProblemDetails(this Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new(error.Type ?? GetDefaultType(error.Status), error.Title ?? GetDefaultTitle(error.Status), error.Status, error.Detail, error.Instance, error.Errors?.ToDictionary(e => e.Key, e => e.Value));
    }

}
