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

using Microsoft.AspNetCore.Diagnostics;

namespace CloudStreams.Core.Api.Services;

/// <summary>
/// Represents the <see cref="IExceptionHandler"/> used to turn unhandled <see cref="ProblemDetailsException"/>s into the problem response they describe
/// </summary>
/// <remarks>
/// Without it, a <see cref="ProblemDetailsException"/> that escapes a request handler, such as a patch rejected by the resource repository, surfaces as an
/// opaque 500 Internal Server Error instead of the 4xx problem it carries, and the client cannot tell the user what went wrong (see #109)
/// </remarks>
/// <param name="logger">The service used to perform logging</param>
public class ProblemDetailsExceptionHandler(ILogger<ProblemDetailsExceptionHandler> logger)
    : IExceptionHandler
{

    /// <inheritdoc/>
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not ProblemDetailsException problemDetailsException || httpContext.Response.HasStarted) return false;
        var problem = problemDetailsException.Problem;
        var status = problem.Status is >= 400 and < 600 ? problem.Status : (int)HttpStatusCode.InternalServerError;
        if (status >= 500) logger.LogError(exception, "An error occurred while processing the request: {problem}", problem.Detail ?? problem.Title);
        else logger.LogDebug("The request was rejected with status {status}: {problem}", status, problem.Detail ?? problem.Title);
        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(problem, (System.Text.Json.JsonSerializerOptions?)null, "application/problem+json", cancellationToken).ConfigureAwait(false);
        return true;
    }

}
