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

using Neuroglia.Mediation.AspNetCore;

namespace CloudStreams.Core.Api;

/// <summary>
/// Defines extensions for <see cref="ControllerBase"/>s
/// </summary>
public static class ControllerBaseExtensions
{

    /// <summary>
    /// Creates a new <see cref="IActionResult"/> that describes the specified failed <see cref="IOperationResult"/> with problem details
    /// </summary>
    /// <param name="controller">The extended <see cref="ControllerBase"/></param>
    /// <param name="result">The failed <see cref="IOperationResult"/> to describe</param>
    /// <returns>A new <see cref="IActionResult"/></returns>
    /// <remarks>
    /// The default processing turns every failure into a generic problem that omits the errors of the result, and with them the reason of the failure. The first error
    /// is therefore returned as is, so that clients know why their request failed
    /// </remarks>
    public static IActionResult Problem(this ControllerBase controller, IOperationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var error = result.Errors?.FirstOrDefault();
        if (error == null) return controller.Process(result);
        var problem = new Neuroglia.ProblemDetails(error.Type, error.Title, error.Status, error.Detail, error.Instance, error.Errors?.ToDictionary(e => e.Key, e => e.Value));
        return new ObjectResult(problem) { StatusCode = result.Status, ContentTypes = { "application/problem+json" } };
    }

}
