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

namespace CloudStreams.Core.Api;

/// <summary>
/// Represents the base class for all Cloud Streams <see cref="ControllerBase"/> implementations
/// </summary>
public abstract class ApiController
    : ControllerBase
{

    /// <summary>
    /// Initializes a new <see cref="ApiController"/>
    /// </summary>
    /// <param name="mediator">The service used to mediate calls</param>
    protected ApiController(IMediator mediator)
    {
        this.Mediator = mediator;
    }

    /// <summary>
    /// Gets the service used to mediate calls
    /// </summary>
    protected IMediator Mediator { get; }

    /// <summary>
    /// Processes the specified <see cref="Response"/>
    /// </summary>
    /// <param name="response">The <see cref="Response"/> to process</param>
    /// <returns>A new <see cref="IActionResult"/></returns>
    protected virtual IActionResult Process(Response response)
    {
        if (response.IsSuccessStatusCode()) return new ObjectResult(response.Content) { StatusCode = response.Status };
        return new ObjectResult(response) { StatusCode = response.Status };
    }

    /// <summary>
    /// Processes the specified <see cref="Response"/>
    /// </summary>
    /// <typeparam name="TContent">The expected type of result</typeparam>
    /// <param name="response">The <see cref="Response"/> to process</param>
    /// <returns>A new <see cref="IActionResult"/></returns>
    protected virtual IActionResult Process<TContent>(Response<TContent> response)
    {
        if (response.IsSuccessStatusCode()) return new ObjectResult(response.Content) { StatusCode = response.Status };
        return new ObjectResult(response) { StatusCode = response.Status };
    }

}
