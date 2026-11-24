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

namespace CloudStreams.Core.Api;

/// <summary>
/// Represents the base class for all <see cref="ApiController"/>s
/// </summary>
/// <remarks>
/// Initializes a new <see cref="ApiController"/>
/// </remarks>
/// <param name="mediator">The service used to mediate calls</param>
public abstract class ApiController(IMediator mediator)
    : ControllerBase
{

    /// <summary>
    /// Gets the service used to mediate calls
    /// </summary>
    protected IMediator Mediator { get; } = mediator;

    /// <summary>
    /// Creates a new <see cref="IActionResult"/> that describes the specified <see cref="IOperationResult"/>
    /// </summary>
    /// <param name="result">The <see cref="IOperationResult"/> to describe</param>
    /// <returns>A new <see cref="IActionResult"/></returns>
    /// <remarks>
    /// Successful results are processed as usual. Failed ones are described with problem details, which is what clients, including the API client and the dashboard,
    /// expect: by default, they are described with a bare dictionary of errors, which clients read as a problem with neither status nor detail (see #204)
    /// </remarks>
    protected virtual IActionResult Process(IOperationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.Status is >= 200 and < 300) return Neuroglia.Mediation.AspNetCore.ControllerBaseExtensions.Process(this, result);
        return this.Problem(result);
    }

}