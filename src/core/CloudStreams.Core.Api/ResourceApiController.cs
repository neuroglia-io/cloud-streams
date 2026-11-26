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
/// Represents the base class of the controllers used to manage <see cref="IResource"/>s
/// </summary>
/// <typeparam name="TResource">The type of <see cref="IResource"/> to manage</typeparam>
/// <param name="mediator">The service used to mediate calls</param>
public abstract class ResourceApiController<TResource>(IMediator mediator)
    : ApiController(mediator)
    where TResource : class, IResource, new()
{

    /// <summary>
    /// Attempts to parse the specified label selectors
    /// </summary>
    /// <param name="labelSelector">The comma-separated label selectors to parse</param>
    /// <param name="labelSelectors">The parsed label selectors, if any</param>
    /// <returns>A boolean indicating whether or not the label selectors could be parsed</returns>
    protected virtual bool TryParseLabelSelectors(string? labelSelector, out IEnumerable<LabelSelector>? labelSelectors)
    {
        labelSelectors = null;
        try
        {
            if (!string.IsNullOrWhiteSpace(labelSelector)) labelSelectors = LabelSelector.ParseList(labelSelector);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Creates a new <see cref="IActionResult"/> describing an invalid label selector
    /// </summary>
    /// <param name="labelSelector">The invalid label selector</param>
    /// <returns>A new <see cref="IActionResult"/></returns>
    protected IActionResult InvalidLabelSelector(string labelSelector)
    {
        this.ModelState.AddModelError(nameof(labelSelector), $"The specified value '{labelSelector}' is not a valid comma-separated label selector list");
        return this.ValidationProblem("Bad Request", statusCode: (int)HttpStatusCode.BadRequest, title: "Bad Request", modelStateDictionary: this.ModelState);
    }

}
